using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class TransferSecurityDepositHandler(IRepository<DepositTransaction> transactions, IReadRepository<Lease> leases, SecurityDepositLedger ledger, IUnitOfWork uow)
    : ICommandHandler<TransferSecurityDepositCommand, DepositDto>
{
    public Task<DepositDto> Handle(TransferSecurityDepositCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Same tenant and legal entity, an open receiving lease, and something held.
        if (cmd.FromLeaseId == cmd.ToLeaseId) throw new FieldValidationException("toLeaseId", "Choose a different lease.");
        var from = await leases.GetAsync(cmd.FromLeaseId, token);
        var to = await leases.GetAsync(cmd.ToLeaseId, token);
        if (from.LesseePartyId != to.LesseePartyId || from.LegalEntityId != to.LegalEntityId) throw new ConflictException("A deposit can only roll over to a lease of the same tenant and legal entity.");
        if (to.Status is LeaseStatus.Cancelled or LeaseStatus.Ended or LeaseStatus.Terminated) throw new ConflictException("The receiving lease is closed.");
        var held = (await ledger.BuildAsync(from, token)).Held;
        if (held <= 0) throw new ConflictException("There is no deposit to transfer.");

        // 2. Move it in the register.
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        // Both leases keep the money in the same liability account, so only the deposit register changes.
        transactions.AddRange([
            new DepositTransaction { LegalEntityId = from.LegalEntityId, LeaseId = from.Id, Date = today, Kind = DepositKind.TransferOut, Amount = held, Description = $"Rolled over to {to.Number}" },
            new DepositTransaction { LegalEntityId = to.LegalEntityId, LeaseId = to.Id, Date = today, Kind = DepositKind.TransferIn, Amount = held, Description = $"Rolled over from {from.Number}" },
        ]);
        await transactions.SaveChangesAsync(token);
        return await ledger.BuildAsync(to, token);
    }, ct);
}
