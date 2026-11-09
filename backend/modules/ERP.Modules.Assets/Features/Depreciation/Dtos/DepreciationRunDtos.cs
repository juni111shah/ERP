using System.Linq.Expressions;
using ERP.Modules.Assets.Domain;

namespace ERP.Modules.Assets.Features;

public sealed record DepreciationEntryDto(Guid AssetId, string Number, string Name, decimal Amount, decimal AccumulatedAfter);

public sealed record DepreciationRunDto(
    Guid Id, Guid LegalEntityId, int Year, int Month, DateOnly PeriodEnd, Guid? JournalEntryId, decimal Total, int AssetCount, DateTime RunAt, Guid? RunBy,
    IReadOnlyList<DepreciationEntryDto>? Entries, bool AlreadyRun)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<DepreciationRun, DepreciationRunDto>> Projection =
        r => new DepreciationRunDto(r.Id, r.LegalEntityId, r.Year, r.Month, r.PeriodEnd, r.JournalEntryId, r.Total, r.AssetCount, r.RunAt, r.RunBy, null, true);

    public static DepreciationRunDto From(DepreciationRun r, IReadOnlyList<DepreciationEntryDto>? entries, bool alreadyRun)
        => new(r.Id, r.LegalEntityId, r.Year, r.Month, r.PeriodEnd, r.JournalEntryId, r.Total, r.AssetCount, r.RunAt, r.RunBy, entries, alreadyRun);
}
