using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>The X report of an open shift, or the Z report of a closed one.</summary>
public record GetPosShiftReportQuery(Guid SessionId) : IQuery<PosShiftReportDto>;
