using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>One counter return with its lines and refunds.</summary>
public record GetPosReturnByIdQuery(Guid Id) : IQuery<PosReturnDto>;
