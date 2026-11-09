using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Throws away a held ticket.</summary>
public record DiscardParkedOrderCommand(Guid OrderId) : ICommand;
