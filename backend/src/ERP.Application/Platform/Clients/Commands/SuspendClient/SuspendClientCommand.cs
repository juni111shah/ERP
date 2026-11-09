using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Suspends a client by hand (data is kept) and tells them.</summary>
public sealed record SuspendClientCommand(Guid OrgId, string? Reason) : ICommand<ClientDetail>;
