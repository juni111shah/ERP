using ERP.Application.Common;

namespace ERP.Modules.Crm.Features;

/// <summary>Marks an interaction resolved.</summary>
public record ResolveInteractionCommand(Guid Id) : ICommand<bool>;
