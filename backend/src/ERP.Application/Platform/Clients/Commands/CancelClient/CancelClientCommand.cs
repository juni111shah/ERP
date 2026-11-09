using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Cancels a client's subscription. Their data is kept.</summary>
public sealed record CancelClientCommand(Guid OrgId, string? Reason) : ICommand<ClientDetail>;
