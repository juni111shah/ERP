using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>The open shift of a register with its drawer movements; null when the register has none.</summary>
public record GetCurrentSessionQuery(Guid RegisterId) : IQuery<PosSessionDto?>;
