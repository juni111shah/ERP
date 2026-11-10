using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>The latest held tickets, of one register or all of them.</summary>
public record GetHeldOrdersQuery(Guid? RegisterId) : IQuery<List<PosOrderDto>>;
