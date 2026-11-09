using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Creates a client by hand: workspace, first administrator (with a temporary password when the account is new) and subscription.</summary>
public sealed record CreateClientCommand(CreateClientInput Input) : ICommand<CreatedClient>;
