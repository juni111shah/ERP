using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Lifts a suspension or cancellation; the client goes back to the state its invoices say.</summary>
public sealed record ReactivateClientCommand(Guid OrgId) : ICommand<ClientDetail>;
