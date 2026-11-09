using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Reminders;

internal sealed class SendRemindersNowHandler(ReminderDispatcher dispatcher, IClock clock) : ICommandHandler<SendRemindersNowCommand, int>
{
    public Task<int> Handle(SendRemindersNowCommand command, CancellationToken ct) => dispatcher.RunAsync(DateOnly.FromDateTime(clock.UtcNow), ct);
}
