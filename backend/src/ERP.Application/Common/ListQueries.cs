using System.Linq.Expressions;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Application.Common;

/// <summary>
/// A generic list request. One open-generic handler (<see cref="ListRequestHandler{TRequest,TResponse}"/>) serves every closed
/// request type, so adding a list never needs a new handler class. The request knows its entity type and resolves the repository.
/// </summary>
public interface IListRequest<TResponse> : IRequest<TResponse>
{
    Task<TResponse> ExecuteAsync(IServiceProvider services, CancellationToken ct);
}

/// <summary>
/// Paged list of any entity: search / filter / sort / paging come from the entity's [Searchable]/[Filterable]/[Sortable]
/// whitelist. Endpoints send this instead of writing a query per table (see <c>MapPagedList</c>).
/// </summary>
public sealed record ListQuery<T, TDto>(PagedQuery Paging, Expression<Func<T, TDto>> Projection, string Permission)
    : IListRequest<PagedResult<TDto>>, IQuery<PagedResult<TDto>>, IRequirePermission where T : Entity
{
    public string RequiredPermission => Permission;

    public Task<PagedResult<TDto>> ExecuteAsync(IServiceProvider services, CancellationToken ct)
        => services.GetRequiredService<IReadRepository<T>>().PageAsync(Paging, Projection, ct: ct);
}

/// <summary>Same, for entities with custom fields: stored values are projected through the entity type's definitions.</summary>
public sealed record ListWithCustomFieldsQuery<T, TDto>(PagedQuery Paging, string CustomFieldEntityType, Func<T, JsonObject, TDto> Map, string Permission)
    : IListRequest<PagedResult<TDto>>, IQuery<PagedResult<TDto>>, IRequirePermission where T : Entity, IHasCustomFields
{
    public string RequiredPermission => Permission;

    public async Task<PagedResult<TDto>> ExecuteAsync(IServiceProvider services, CancellationToken ct)
    {
        var page = await services.GetRequiredService<IReadRepository<T>>().PageAsync(Paging, ct: ct);
        var defs = await services.GetRequiredService<ICustomFieldService>().GetDefinitionsAsync(CustomFieldEntityType, ct);
        return page.Map(x => Map(x, CustomFieldValidator.Project(x.CustomFields, defs)));
    }
}

internal sealed class ListRequestHandler<TRequest, TResponse>(IServiceProvider services) : IRequestHandler<TRequest, TResponse>
    where TRequest : IListRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, CancellationToken ct) => request.ExecuteAsync(services, ct);
}
