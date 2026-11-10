using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Starts a paid period from today, optionally issuing its invoice.</summary>
public sealed record ActivateClientCommand(Guid OrgId, bool IssueInvoice) : ICommand<ClientDetail>;
