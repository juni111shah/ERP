using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>A sale found by its receipt number, as typed or scanned at the counter.</summary>
public record GetPosOrderByNumberQuery(string Number) : IQuery<PosOrderDto>;
