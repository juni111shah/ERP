using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>Routes, today's trips, this month's van sales and collections, and the active wholesale rules.</summary>
public record GetDistributionKpiSummaryQuery : IQuery<DistributionKpiSummaryDto>;
