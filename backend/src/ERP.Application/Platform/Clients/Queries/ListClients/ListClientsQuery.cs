using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The clients, newest first, with search and a state or plan filter (state "none" = no plan, "planRequest" = asked for a change).</summary>
public sealed record ListClientsQuery : PagedQuery, IQuery<PagedResult<ClientRow>>
{
    public string? Status { get; init; }
    public Guid? PlanId { get; init; }
}
