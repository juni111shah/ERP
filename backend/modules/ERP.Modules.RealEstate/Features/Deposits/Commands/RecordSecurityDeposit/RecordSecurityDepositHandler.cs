using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class RecordSecurityDepositHandler(
    IRepository<DepositTransaction> transactions, IReadRepository<Lease> leases, SecurityDepositLedger ledger, IPostingService posting, IUnitOfWork uow)
    : ICommandHandler<RecordSecurityDepositCommand, DepositDto>
{
    public Task<DepositDto> Handle(RecordSecurityDepositCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The lease takes deposits up to the agreed amount, into a bank account.
        var lease = await leases.GetAsync(cmd.LeaseId, token);
        if (lease.Status == LeaseStatus.Cancelled) throw new ConflictException("A cancelled lease cannot take a deposit.");
        var before = await ledger.BuildAsync(lease, token);
        if (lease.SecurityDepositAmount > 0 && before.Received + cmd.Amount > lease.SecurityDepositAmount)
            throw new FieldValidationException("amount", $"The agreed deposit is {lease.SecurityDepositAmount:0.00}; {before.Received:0.00} has already been received.");
        await ledger.RequireBankAsync(cmd.BankAccountId, "bankAccountId", token);

        // 2. Post Dr bank, Cr deposits held.
        var deposits = await posting.AccountForRoleAsync(SecurityDepositLedger.Role, token);
        var memo = $"Security deposit, lease {lease.Number}";
        var journal = await posting.PostAsync(new PostingRequest(lease.LegalEntityId, cmd.Date, "deposit", lease.Id, memo, lease.Number,
            [new PostingLine(cmd.BankAccountId, cmd.Amount, 0, memo), new PostingLine(deposits, 0, cmd.Amount, memo, lease.LesseePartyId)], lease.CurrencyCode), token);

        // 3. Register the movement, save.
        transactions.Add(new DepositTransaction { LegalEntityId = lease.LegalEntityId, LeaseId = lease.Id, Date = cmd.Date, Kind = DepositKind.Received, Amount = cmd.Amount, Description = "Deposit received", JournalEntryId = journal.Id });
        await transactions.SaveChangesAsync(token);
        return await ledger.BuildAsync(lease, token);
    }, ct);
}
