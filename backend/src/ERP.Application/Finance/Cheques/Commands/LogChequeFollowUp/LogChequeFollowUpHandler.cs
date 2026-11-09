using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class LogChequeFollowUpHandler(IPaymentInstrumentRepository instruments, IClock clock, ChequeDtoBuilder dtos) : ICommandHandler<LogChequeFollowUpCommand, ChequeDto>
{
    public async Task<ChequeDto> Handle(LogChequeFollowUpCommand command, CancellationToken ct)
    {
        // 1. Only a bounced cheque that has not been replaced.
        var c = await instruments.GetForUpdateAsync(command.Id, ct);
        if (c.Status != InstrumentStatus.Bounced) throw new ConflictException("Follow-ups are logged against a bounced cheque that has not been replaced.");

        // 2. Record the follow-up (today when no date is given) and save.
        c.FollowUpOn = command.Date ?? DateOnly.FromDateTime(clock.UtcNow);
        c.FollowUpNote = command.Note.Trim();
        await instruments.SaveChangesAsync(ct);
        return await dtos.BuildAsync(c, ct);
    }
}
