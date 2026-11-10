using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Cancels a work order that made nothing, returning its material and reversing its journals.</summary>
public record CancelWorkOrderCommand(Guid Id, string? Reason) : ICommand;
