using TreatmentService.Application.Abstractions;
using TreatmentService.Application.Exceptions;
using TreatmentService.Application.Reminders.Mappers;
using TreatmentService.Application.Reminders.Models;
using TreatmentService.Domain.Entities;
using TreatmentService.Domain.UnitOfWork;

namespace TreatmentService.Application.Reminders.Commands.AddReminder;

public sealed class AddReminderCommandHandler(
    IUserDirectory userDirectory,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddReminderCommandParams, ReminderDto>
{
    public async Task<ReminderDto> HandleAsync(
        AddReminderCommandParams command,
        CancellationToken cancellationToken)
    {
        if (!await userDirectory.ExistsAsync(command.UserId, cancellationToken))
        {
            throw new UserNotFoundException();
        }

        var reminder = Reminder.Create(command.Time, command.UserId);

        await unitOfWork.ReminderRepository.AddAsync(reminder, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return reminder.MapReminderToReminderDto();
    }
}
