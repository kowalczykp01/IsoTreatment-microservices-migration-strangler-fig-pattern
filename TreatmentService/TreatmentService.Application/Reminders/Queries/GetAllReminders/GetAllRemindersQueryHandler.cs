using TreatmentService.Application.Abstractions;
using TreatmentService.Application.Exceptions;
using TreatmentService.Application.Reminders.Mappers;
using TreatmentService.Application.Reminders.Models;
using TreatmentService.Domain.UnitOfWork;

namespace TreatmentService.Application.Reminders.Queries.GetAllReminders;

public sealed class GetAllRemindersQueryHandler(
    IUserDirectory userDirectory,
    IUnitOfWork unitOfWork)
    : IQueryHandler<GetAllRemindersQueryParams, IEnumerable<ReminderDto>>
{
    public async Task<IEnumerable<ReminderDto>> HandleAsync(
        GetAllRemindersQueryParams query,
        CancellationToken cancellationToken)
    {
        if (!await userDirectory.ExistsAsync(query.UserId, cancellationToken))
        {
            throw new UserNotFoundException();
        }

        var reminders = await unitOfWork.ReminderRepository
            .GetAllForUserAsync(query.UserId, cancellationToken);

        return reminders.MapRemindersToReminderDtos();
    }
}
