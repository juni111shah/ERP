using System.Linq.Expressions;
using System.Text.Json.Nodes;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modularity.Web;

/// <summary>
/// Base class of every API controller (core and modules).
/// <para>
/// A controller is the HTTP layer only: it binds the request, sends <b>one</b> command or query through <see cref="Mediator"/>
/// and shapes the HTTP result. Business rules live in the handlers, data access in the repositories:
/// <c>Controller → Command/Query handler → Repository → Database</c>.
/// </para>
/// <para>
/// Return the value itself (<c>Task&lt;InvoiceDto&gt;</c>), <c>Created(...)</c> or <c>NoContent()</c>: the standard
/// <see cref="ApiResponse{T}"/> envelope is added for you by <see cref="ApiEnvelopeResultFilter"/>.
/// </para>
/// </summary>
[ApiController]
public abstract class ApiControllerBase(ISender mediator) : ControllerBase
{
    /// <summary>Sends commands and queries to their handlers.</summary>
    protected ISender Mediator { get; } = mediator;

    /// <summary>
    /// A paged list of any entity with no handler of its own: search, filter, sort and paging come from the entity's
    /// [Searchable]/[Filterable]/[Sortable] attributes, and <paramref name="projection"/> shapes each row.
    /// </summary>
    protected Task<PagedResult<TDto>> PagedList<TEntity, TDto>(PagedQuery query, Expression<Func<TEntity, TDto>> projection, string permission, CancellationToken ct)
        where TEntity : Entity
        => Mediator.Send(new ListQuery<TEntity, TDto>(query, projection, permission), ct);

    /// <summary>Same, for entities with custom fields: stored values are projected through the definitions of <paramref name="customFieldEntityType"/>.</summary>
    protected Task<PagedResult<TDto>> PagedList<TEntity, TDto>(PagedQuery query, string customFieldEntityType, Func<TEntity, JsonObject, TDto> map, string permission, CancellationToken ct)
        where TEntity : Entity, IHasCustomFields
        => Mediator.Send(new ListWithCustomFieldsQuery<TEntity, TDto>(query, customFieldEntityType, map, permission), ct);
}
