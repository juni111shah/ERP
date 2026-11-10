namespace ERP.Application.Finance;

/// <summary>A reduction of what is owed to a vendor that is not a payment. With no bill it is credit held against the vendor.</summary>
public sealed record BillAdjustment(Guid PartyId, string? PartyName, Guid? BillId, decimal Amount);

/// <summary>Lets a module report what it took off payables, such as a purchase return, so ageing agrees with the ledger.</summary>
public interface IBillAdjustmentSource
{
    Task<IReadOnlyList<BillAdjustment>> AsOfAsync(DateOnly asOf, Guid? legalEntityId, Guid? partyId, CancellationToken ct);
}
