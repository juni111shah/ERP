using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>One counter sale with its lines, payments and what is left to refund.</summary>
public record GetPosOrderByIdQuery(Guid Id) : IQuery<PosOrderDto>;
