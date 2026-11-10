using System.Linq.Expressions;
using ERP.Modules.Assets.Domain;

namespace ERP.Modules.Assets.Features;

/// <summary>LifeMonths is optional: the category's life applies when it is left out.</summary>
public sealed record AssetInput(
    Guid LegalEntityId, string Name, Guid CategoryId, DateOnly AcquisitionDate, decimal Cost, decimal SalvageValue = 0m, Guid? BusinessUnitId = null, string? Custodian = null,
    int? LifeMonths = null, string? Description = null);

public sealed record AssetDto(
    Guid Id, Guid LegalEntityId, string Number, string Name, string? Description, Guid CategoryId, DateOnly AcquisitionDate, decimal Cost, decimal SalvageValue, DepreciationMethod Method, int LifeMonths,
    decimal? DecliningRatePercent, Guid? BusinessUnitId, string? Custodian, AssetStatus Status, decimal AccumulatedDepreciation, DateOnly? CapitalisedOn, CapitalisationSource? CapitalisationSource,
    Guid? CapitalisationJournalId, Guid? SourceBillId, Guid? SourceBillLineId, DateOnly? DisposedOn, decimal? DisposalProceeds, decimal? DisposalGainLoss, Guid? DisposalJournalId)
{
    public decimal NetBookValue => Cost - AccumulatedDepreciation;

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<FixedAsset, AssetDto>> Projection =
        a => new AssetDto(a.Id, a.LegalEntityId, a.Number, a.Name, a.Description, a.CategoryId, a.AcquisitionDate, a.Cost, a.SalvageValue, a.Method, a.LifeMonths, a.DecliningRatePercent, a.BusinessUnitId, a.Custodian, a.Status,
            a.AccumulatedDepreciation, a.CapitalisedOn, a.CapitalisationSource, a.CapitalisationJournalId, a.SourceBillId, a.SourceBillLineId, a.DisposedOn, a.DisposalProceeds, a.DisposalGainLoss, a.DisposalJournalId);

    public static AssetDto From(FixedAsset a) => new(
        a.Id, a.LegalEntityId, a.Number, a.Name, a.Description, a.CategoryId, a.AcquisitionDate, a.Cost, a.SalvageValue, a.Method, a.LifeMonths, a.DecliningRatePercent, a.BusinessUnitId, a.Custodian, a.Status,
        a.AccumulatedDepreciation, a.CapitalisedOn, a.CapitalisationSource, a.CapitalisationJournalId, a.SourceBillId, a.SourceBillLineId, a.DisposedOn, a.DisposalProceeds, a.DisposalGainLoss, a.DisposalJournalId);
}

/// <summary>
/// Clearing: AccountId is the clearing account. Bank: AccountId is the bank or cash account. Vendor: PartyId; an open bill is raised, so the payables ledger stays tied to the control account.
/// Bill: BillId, and BillLineId when the bill has several lines; a line already coded to the asset account needs no entry, any other line is moved to it.
/// </summary>
public sealed record CapitaliseInput(CapitalisationSource Source, DateOnly? Date = null, Guid? AccountId = null, Guid? PartyId = null, Guid? BillId = null, Guid? BillLineId = null, string? Reference = null);

/// <summary>ProceedsAccountId is required when proceeds are above zero. GainLossAccountId defaults to the category's.</summary>
public sealed record DisposeInput(DateOnly Date, decimal Proceeds = 0m, Guid? ProceedsAccountId = null, Guid? GainLossAccountId = null, string? Reason = null);
