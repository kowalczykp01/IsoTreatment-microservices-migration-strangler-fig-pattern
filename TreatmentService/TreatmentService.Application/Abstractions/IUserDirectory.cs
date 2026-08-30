namespace TreatmentService.Application.Abstractions;

public interface IUserDirectory
{
    Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default);
}
