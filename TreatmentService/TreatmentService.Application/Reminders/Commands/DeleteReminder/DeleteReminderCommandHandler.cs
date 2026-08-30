using TreatmentService.Application.Abstractions;
using TreatmentService.Application.Exceptions;
using TreatmentService.Domain.UnitOfWork;

namespace TreatmentService.Application.Reminders.Commands.DeleteReminder;

public sealed class DeleteReminderCommandHandler(
    IUserDirectory userDirectory,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteReminderCommandParams>
{
    public async Task HandleAsync(
        DeleteReminderCommandParams command,
        CancellationToken cancellationToken)
    {
        if (!await userDirectory.ExistsAsync(command.UserId, cancellationToken))
        {
            throw new UserNotFoundException();
        }

        var reminder = await unitOfWork.ReminderRepository
            .GetByIdForUserAsync(command.Id, command.UserId, cancellationToken)
            ?? throw new ReminderNotFoundException();

        unitOfWork.ReminderRepository.Remove(reminder);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
