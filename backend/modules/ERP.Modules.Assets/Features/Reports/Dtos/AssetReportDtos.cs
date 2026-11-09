using ERP.Modules.Assets.Domain;

namespace ERP.Modules.Assets.Features;

public sealed record AssetRegisterRow(
    Guid AssetId, Guid LegalEntityId, string Number, string Name, Guid CategoryId, string CategoryName, Guid? BusinessUnitId, string? Custodian, DateOnly AcquisitionDate, DateOnly CapitalisedOn,
    decimal Cost, decimal SalvageValue, decimal AccumulatedDepreciation, decimal NetBookValue);
public sealed record AssetRegisterDto(DateOnly AsOf, IReadOnlyList<AssetRegisterRow> Rows, decimal TotalCost, decimal TotalAccumulatedDepreciation, decimal TotalNetBookValue);

/// <summary>Posted = charged by the run in JournalEntryId (PostedAmount can exceed Amount when the run caught up a skipped month). Planned = what the schedule still expects.</summary>
public sealed record AssetScheduleRow(int Year, int Month, DateOnly PeriodEnd, decimal Amount, decimal Accumulated, decimal NetBookValue, bool Posted, decimal? PostedAmount, Guid? RunId, Guid? JournalEntryId);
public sealed record AssetScheduleDto(Guid AssetId, string Number, string Name, decimal Cost, decimal SalvageValue, DepreciationMethod Method, int LifeMonths, AssetStatus Status, decimal AccumulatedDepreciation, IReadOnlyList<AssetScheduleRow> Rows);

public sealed record DepreciationPeriodRow(int Year, int Month, decimal Posted, decimal Planned);
public sealed record DepreciationForecastDto(DateOnly From, DateOnly To, IReadOnlyList<DepreciationPeriodRow> Rows, decimal TotalPosted, decimal TotalPlanned);
