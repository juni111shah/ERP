using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>The active registers (with their open shift) and the till settings.</summary>
public record GetPosTerminalQuery : IQuery<PosTerminalDto>;
