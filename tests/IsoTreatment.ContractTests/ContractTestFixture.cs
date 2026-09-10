using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace IsoTreatment.ContractTests;

public sealed class ContractTestFixture : IAsyncLifetime
{
    private readonly List<int> _seededUserIds = new();

    public ContractTestSettings Settings { get; } = ContractTestSettings.FromEnvironment();

    public async Task InitializeAsync()
    {
        await EnsureReachableAsync(ServiceUnderTest.Gateway);
        await EnsureReachableAsync(ServiceUnderTest.Monolith);
        await EnsureReachableAsync(ServiceUnderTest.Treatment);
        await EnsureDatabaseReachableAsync();
    }

    public async Task DisposeAsync()
    {
        if (_seededUserIds.Count == 0)
        {
            return;
        }

        var ids = string.Join(",", _seededUserIds);

        await using var connection = new SqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"DELETE FROM [Reminders] WHERE [UserId] IN ({ids});"
            + $"DELETE FROM [Entries] WHERE [UserId] IN ({ids});"
            + $"DELETE FROM [Users] WHERE [Id] IN ({ids});";
        await command.ExecuteNonQueryAsync();
    }

    public string BaseAddressOf(string service) => service switch
    {
        ServiceUnderTest.Gateway => Settings.GatewayBaseAddress,
        ServiceUnderTest.Monolith => Settings.MonolithBaseAddress,
        ServiceUnderTest.Treatment => Settings.TreatmentBaseAddress,
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown service.")
    };

    public async Task<int> SeedUserAsync()
    {
        await using var connection = new SqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [Users]
                ([FirstName], [LastName], [Email], [PasswordHash], [EmailConfirmed],
                 [Weight], [ClimaxDoseInMiligramsPerKilogramOfBodyWeight], [DailyDose],
                 [MedicationStartDate])
            OUTPUT INSERTED.[Id]
            VALUES
                ('Contract', 'Test', @email, 'irrelevant-for-contract-tests', 1,
                 70, 120, 40, '2024-01-01');
            """;
        command.Parameters.AddWithValue("@email", $"contract-{Guid.NewGuid():N}@example.com");

        var userId = (int)(await command.ExecuteScalarAsync())!;
        _seededUserIds.Add(userId);
        return userId;
    }

    public ReminderApiClient ClientFor(string service, int userId) =>
        new(BaseAddressOf(service), CreateToken(userId));

    public ReminderApiClient AnonymousClientFor(string service) =>
        new(BaseAddressOf(service), token: null);

    public ReminderApiClient ClientWithRawTokenFor(string service, string token) =>
        new(BaseAddressOf(service), token);

    public string CreateToken(int userId, string? signingKey = null, string? issuer = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? Settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer ?? Settings.Issuer,
            audience: Settings.Audience,
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            expires: DateTime.Now.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task EnsureReachableAsync(string service)
    {
        var baseAddress = BaseAddressOf(service);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            await client.GetAsync($"{baseAddress}/api/reminder");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"The '{service}' service is not answering at {baseAddress}. "
                + "These tests compare two running services; start the stack with "
                + "'docker compose up -d' first.", exception);
        }
    }

    private async Task EnsureDatabaseReachableAsync()
    {
        try
        {
            await using var connection = new SqlConnection(Settings.ConnectionString);
            await connection.OpenAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Cannot reach the database the two services share. Start the stack and apply "
                + "migrations, then export the secrets with 'set -a; . ./.env; set +a'.", exception);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ContractTestCollection : ICollectionFixture<ContractTestFixture>
{
    public const string Name = "Contract";
}
