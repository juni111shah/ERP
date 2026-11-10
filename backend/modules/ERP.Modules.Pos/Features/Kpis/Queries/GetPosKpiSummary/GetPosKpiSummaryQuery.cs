using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Today's counter sales, returns, average ticket and the active registers and open shifts.</summary>
public record GetPosKpiSummaryQuery(Guid? LegalEntityId = null) : IQuery<PosKpiSummaryDto>;
