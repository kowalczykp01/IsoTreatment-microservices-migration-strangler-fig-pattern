using AutoMapper;
using IsoTreatmentProcessSupportAPI.Entities;
using IsoTreatmentProcessSupportAPI.Exceptions;
using IsoTreatmentProcessSupportAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IsoTreatmentProcessSupportAPI.Services
{
    public interface IUserService
    {
        void RegisterUser(RegisterUserDto dto);
        void ConfirmEmail(string emailConfirmationToken);
        string GenerateEmailToken(string userEmail);
        string GenerateLoginToken(LoginDto dto);
        UserDto GetUserInfo(string token);
        UserDto UpdateUserInfo(string token, UserDto dto);
        void ForgotPassword(string email);
        void ResetPassword(string token, ResetPasswordDto dto);
    }
    public class UserService : IUserService
    {
        private readonly IsoSupportDbContext _dbContext;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IMailkitService _mailkitService;
        private readonly AuthenticationSettings _authenticationSettings;
        private readonly ITokenService _tokenService;
        private readonly IMapper _mapper;

        public UserService(IsoSupportDbContext dbContext, IPasswordHasher<User> passwordHasher, IMailkitService mailkitService,
            AuthenticationSettings authenticationSettings, ITokenService tokenService, IMapper mapper)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _mailkitService = mailkitService;
            _authenticationSettings = authenticationSettings;
            _tokenService = tokenService;
            _mapper = mapper;
        }

        public UserDto GetUserInfo(string token)
        {
            int userId = _tokenService.GetUserIdFromToken(token);

            var user = _dbContext.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            var userDto = _mapper.Map<UserDto>(user);

            return userDto;
        }

        public UserDto UpdateUserInfo(string token, UserDto dto)
        {
            int userId = _tokenService.GetUserIdFromToken(token);

            var user = _dbContext.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (user.FirstName != dto.FirstName)
            {
                user.FirstName = dto.FirstName;
            }

            if (user.LastName != dto.LastName)
            {
                user.LastName = dto.LastName;
            }

            if (user.Email != dto.Email)
            {
                user.Email = dto.Email;
                user.EmailConfirmed = false;
                _dbContext.SaveChanges();

                var emailConfirmationToken = GenerateEmailToken(dto.Email);

                _mailkitService.SendEmailConfirmationMail(dto.Email, emailConfirmationToken);
            }

            if (user.Weight != dto.Weight)
            {
                user.Weight = dto.Weight;
            }

            _dbContext.SaveChanges();

            var updatedUserInfo = _mapper.Map<UserDto>(user);

            return updatedUserInfo;
        }

        public void RegisterUser(RegisterUserDto dto)
        {
            var newUser = new User()
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Weight = dto.Weight,
                ClimaxDoseInMiligramsPerKilogramOfBodyWeight = dto.ClimaxDoseInMiligramsPerKilogramOfBodyWeight,
                DailyDose = dto.DailyDose,
                MedicationStartDate = dto.MedicationStartDate
            };
            var hashedPassword = _passwordHasher.HashPassword(newUser, dto.Password);
            
            newUser.PasswordHash = hashedPassword;
            _dbContext.Users.Add(newUser);

            var emailConfirmationToken = GenerateEmailToken(dto.Email);

            _mailkitService.SendEmailConfirmationMail(newUser.Email, emailConfirmationToken);

            _dbContext.SaveChanges();
        }

        public void ForgotPassword(string email)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.Email == email);

            if (user is null)
            {
                throw new NotFoundException("User not found");
            }

            var resetPasswordToken = GenerateEmailToken(email);
            user.ResetPasswordToken = resetPasswordToken;

            _dbContext.SaveChanges();

            _mailkitService.SendResetPasswordMail(user.Email, resetPasswordToken);
        }

        public void ResetPassword(string token, ResetPasswordDto dto)
        {
            var userEmail = DecodeUserEmailFromToken(token);

            var user = _dbContext
                .Users
                .FirstOrDefault(e => e.Email == userEmail);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (user.ResetPasswordToken != token)
            {
                throw new BadRequestException("Cannot reset password");
            }

            var hashedPassword = _passwordHasher.HashPassword(user, dto.NewPassword);

            user.PasswordHash = hashedPassword;

            _dbContext.SaveChanges();
        }

        public void ConfirmEmail(string emailConfirmationToken)
        {
            var userEmail = DecodeUserEmailFromToken(emailConfirmationToken);

            var user = _dbContext
                .Users
                .FirstOrDefault(e => e.Email == userEmail);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (user.EmailConfirmed)
            {
                throw new BadRequestException("Email has already been confirmed");
            }


            user.EmailConfirmed = true;
            _dbContext.SaveChanges();
        }

        public string GenerateEmailToken(string email)
        {
            var claims = new List<Claim>()
            {
                new Claim(ClaimTypes.Email, $"{email}")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authenticationSettings.SigningKey));
            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.Add(_authenticationSettings.Expiry ?? TimeSpan.FromHours(1));

            var token = new JwtSecurityToken(_authenticationSettings.Issuer,
                _authenticationSettings.Issuer,
                claims,
                expires: expires,
                signingCredentials: cred);

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(token);
        }

        public string GenerateLoginToken(LoginDto dto)
        {
            var user = _dbContext.Users
                .FirstOrDefault(u => u.Email == dto.Email);

            if (user is null)
            {
                throw new BadRequestException("Invalid username or password");
            }

            if (user.EmailConfirmed == false)
            {
                throw new BadRequestException("Email is not confirmed");
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                throw new BadRequestException("Invalid username or password");
            }

            var issuer = _authenticationSettings.Issuer;
        var audience = _authenticationSettings.Audience;
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()) };

        var expires = DateTime.Now.Add(_authenticationSettings.Expiry ?? TimeSpan.FromHours(1));

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authenticationSettings.SigningKey)),
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: signingCredentials
        );

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(token);
        }

        public string DecodeUserEmailFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadToken(token) as JwtSecurityToken;

            if (securityToken != null)
            {
                var userEmailClaim = securityToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Email);

                if (userEmailClaim != null)
                {
                    return userEmailClaim.Value;
                }
            }
            return null;
        }
    }
}
