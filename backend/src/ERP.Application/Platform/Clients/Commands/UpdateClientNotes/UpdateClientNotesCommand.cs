using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Changes a client's billing email, phone and notes.</summary>
public sealed record UpdateClientNotesCommand(Guid OrgId, ClientNotesInput Input) : ICommand<ClientDetail>;
