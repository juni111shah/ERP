using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class BounceChequeHandler(
    IPaymentInstrumentRepository instruments,
    ISettlementRepository settlements,
    ISettlementService settlementService,
    ILegalEntityGuard guard,
    IClock clock,
    ICurrentUser user,
    INotifier notifier,
    ERP.Application.Reminders.PermissionAudience audience,
    ChequeDtoBuilder dtos)
    : ICommandHandler<BounceChequeCommand, ChequeDto>
{
    public async Task<ChequeDto> Handle(BounceChequeCommand command, CancellationToken ct)
    {
        // 1. Only a cheque in custody or deposited, not before it was received or deposited.
        var c = await instruments.GetForUpdateAsync(command.Id, ct);
        if (c.Status is not (InstrumentStatus.InCustody or InstrumentStatus.Deposited)) throw new ConflictException("Only a cheque in custody or deposited can bounce.");
        if (command.Date < (c.DepositedOn ?? c.ReceivedOn)) throw new FieldValidationException("date", "A cheque cannot bounce before it was received or deposited.");

        // 2. Reverse its receipt and mark the cheque bounced.
        var s = await settlements.GetWithAllocationsForUpdateAsync(c.SettlementId, ct);
        await settlementService.ReverseAsync(s, $"Bounced cheque {c.InstrumentNo}", ct);
        s.Status = DocumentStatus.Voided;
        c.Status = InstrumentStatus.Bounced; c.BounceReason = command.Reason.Trim(); c.BouncedOn = command.Date;
        VoidMemo.Stamp(s, $"Cheque {c.InstrumentNo} bounced: {c.BounceReason}", clock, user);
        VoidMemo.MarkIfLocked(s, s.Date, (await guard.RequireAsync(s.LegalEntityId, ct)).BooksLockedTo, clock);
        await instruments.SaveChangesAsync(ct);

        // 3. Follow-up: the people who handle cheques are told once, right away; a reminder nags later if nobody logs a follow-up (see CoreReminderSource).
        foreach (var userId in await audience.UsersWithAsync(CorePermissions.ChequesApprove, ct))
            await notifier.NotifyUserAsync(userId, $"Cheque {c.InstrumentNo} bounced", $"{c.PartyName}: {c.Amount:N2} {c.CurrencyCode}. {c.BounceReason}", "/finance/cheques", "error", ct);
        return await dtos.BuildAsync(c, ct);
    }
}
