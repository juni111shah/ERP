using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Workflow;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class CreatePaymentHandler(
    ISettlementRepository settlements,
    PaymentIssuing issuing,
    IApprovalService approvals,
    IUnitOfWork uow,
    PaymentDtoBuilder dtos)
    : ICommandHandler<CreatePaymentCommand, PaymentDto>
{
    public Task<PaymentDto> Handle(CreatePaymentCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The vendor, the bank account and the new payment.
        var i = command.Input;
        var (entity, party, bank) = await issuing.ResolveAsync(i, token);
        var settlement = new Settlement
        {
            LegalEntityId = entity.Id, Direction = InstrumentDirection.Issued, PartyId = party.Id, PartyName = party.Name, Date = i.Date, Method = i.Method, BankAccountId = bank.Id,
            CurrencyCode = entity.BaseCurrency, Amount = i.Amount, Unallocated = i.Amount, Reference = i.Reference, Status = DocumentStatus.Issued, Number = "pending",
        };
        settlements.Add(settlement);

        // 2. Maker-checker: with a matching policy nothing reaches the ledger yet. The row waits, unnumbered and unposted; PostPayment finishes it after the approval.
        if (await approvals.RequiresApprovalAsync(ApprovalDocumentTypes.Payment, i.Amount, token))
        {
            await issuing.CheckAllocationsAsync(settlement, i.Allocations ?? [], token);
            settlement.Status = DocumentStatus.PendingApproval;
            settlement.Number = "DRAFT-" + settlement.Id.ToString("N")[^8..];
            settlement.PendingInput = System.Text.Json.JsonSerializer.Serialize(i);
            await issuing.SubmitForApprovalAsync(settlement, token);
            return await dtos.BuildAsync(settlement, token, []);
        }

        // 3. Otherwise number, allocate and post it now.
        return await issuing.IssueAsync(settlement, i, entity, party, bank, token);
    }, ct);
}
