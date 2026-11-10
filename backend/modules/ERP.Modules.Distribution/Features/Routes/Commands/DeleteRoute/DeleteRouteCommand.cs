using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>Deletes a delivery route.</summary>
public record DeleteRouteCommand(Guid Id) : ICommand;
