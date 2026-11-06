using ERP.Domain.Common;

namespace ERP.Modules.Assets.Domain;

public enum DepreciationMethod { StraightLine, DecliningBalance }

public enum AssetStatus { Draft, Capitalised, FullyDepreciated, Disposed }

/// <summary>Where the cost of a capitalised asset was credited.</summary>
public enum CapitalisationSource { Clearing, Bank, Vendor, Bill }

/// <summary>Default life and method, and the ledger accounts of a kind of asset. Shared by the legal entities of a workspace, as the chart of accounts is.</summary>
[DefaultSort("code")]
public class AssetCategory : OrgEntity
{
    [Searchable, Sortable]
    public string Code { get; set; } = string.Empty;
    [Searchable, Sortable]
    public string Name { get; set; } = string.Empty;
    public int LifeMonths { get; set; }
    [Filterable]
    public DepreciationMethod Method { get; set; }
    /// <summary>Annual rate of the declining balance method. Empty means double declining: twice the straight-line rate of the life.</summary>
    public decimal? DecliningRatePercent { get; set; }
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public Guid DepreciationExpenseAccountId { get; set; }
    /// <summary>Default for the gain or loss on disposal; a disposal can name another account.</summary>
    public Guid? DisposalGainLossAccountId { get; set; }
    [Filterable]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One asset in the register. Method, life and rate are copied from the category when the asset is created, so changing a category never rewrites the history of
/// assets already in it. Accumulated depreciation is kept in step by depreciation runs; each run also leaves a <see cref="DepreciationEntry"/>.
/// </summary>
[DefaultSort("number")]
public class FixedAsset : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;
    [Searchable, Sortable]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Filterable]
    public Guid CategoryId { get; set; }
    [Sortable, Filterable]
    public DateOnly AcquisitionDate { get; set; }
    [Sortable]
    public decimal Cost { get; set; }
    public decimal SalvageValue { get; set; }
    public DepreciationMethod Method { get; set; }
    public int LifeMonths { get; set; }
    public decimal? DecliningRatePercent { get; set; }
    [Filterable]
    public Guid? BusinessUnitId { get; set; }
    [Searchable]
    public string? Custodian { get; set; }
    [Filterable]
    public AssetStatus Status { get; set; } = AssetStatus.Draft;
    public decimal AccumulatedDepreciation { get; set; }

    public DateOnly? CapitalisedOn { get; set; }
    public CapitalisationSource? CapitalisationSource { get; set; }
    /// <summary>The entry that put the cost on the asset account: this module's own, or the linked bill's when that already did.</summary>
    public Guid? CapitalisationJournalId { get; set; }
    [Filterable]
    public Guid? SourceBillId { get; set; }
    public Guid? SourceBillLineId { get; set; }

    public DateOnly? DisposedOn { get; set; }
    public decimal? DisposalProceeds { get; set; }
    /// <summary>Positive = gain, negative = loss.</summary>
    public decimal? DisposalGainLoss { get; set; }
    public Guid? DisposalJournalId { get; set; }
    public string? DisposalReason { get; set; }
}

/// <summary>The monthly depreciation run of a legal entity: at most one per period, with one journal.</summary>
[DefaultSort("-periodEnd")]
public class DepreciationRun : LegalEntityScopedEntity
{
    [Filterable, Sortable]
    public int Year { get; set; }
    [Filterable, Sortable]
    public int Month { get; set; }
    [Sortable]
    public DateOnly PeriodEnd { get; set; }
    /// <summary>Null when nothing was due.</summary>
    public Guid? JournalEntryId { get; set; }
    public decimal Total { get; set; }
    public int AssetCount { get; set; }
    public DateTime RunAt { get; set; }
    public Guid? RunBy { get; set; }
}

/// <summary>What one run charged one asset (a month that was skipped is caught up by the next run).</summary>
public class DepreciationEntry : LegalEntityScopedEntity
{
    public Guid RunId { get; set; }
    public Guid AssetId { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public decimal AccumulatedAfter { get; set; }
}
