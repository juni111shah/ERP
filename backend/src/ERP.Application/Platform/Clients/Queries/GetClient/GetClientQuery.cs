using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>One client's page: subscription, usage, members, invoices and recent activity.</summary>
public sealed record GetClientQuery(Guid OrgId) : IQuery<ClientDetail>;
