using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Active BOMs, open work orders and this month's output, yield and cost.</summary>
public record GetManufacturingKpiSummaryQuery : IQuery<ManufacturingKpiSummaryDto>;
