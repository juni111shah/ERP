using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>One cash register; null when it does not exist.</summary>
public record GetRegisterByIdQuery(Guid Id) : IQuery<PosRegisterDto?>;
