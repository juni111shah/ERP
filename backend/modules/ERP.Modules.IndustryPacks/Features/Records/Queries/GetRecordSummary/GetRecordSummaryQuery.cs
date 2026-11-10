using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Count and amount per status of each record type of a pack the caller may read.</summary>
public sealed record GetRecordSummaryQuery(string Module, Guid? LegalEntityId) : IQuery<IReadOnlyList<RecordTypeSummary>>;
