using System.Text.Json.Nodes;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Customization;

/// <summary>Custom field definitions and the custom-field values of any customizable record.</summary>
[Route("api/v1/custom-fields")]
public sealed class CustomFieldsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("entity-types")]
    [RequirePermission(CorePermissions.CustomFieldsRead)]
    public Task<IReadOnlyList<CustomizableEntityDto>> ListEntityTypes(CancellationToken ct)
        => Mediator.Send(new ListCustomizableEntitiesQuery(), ct);

    [HttpGet]
    [RequirePermission(CorePermissions.CustomFieldsRead)]
    public Task<IReadOnlyList<CustomFieldDefinitionDto>> List(string entityType, bool? includeInactive, CancellationToken ct)
        => Mediator.Send(new ListCustomFieldDefinitionsQuery(entityType, includeInactive ?? false), ct);

    /// <summary>Form metadata: active definitions for rendering, readable by anyone who can read the entity type (checked by the handler).</summary>
    [HttpGet("form/{entityType}")]
    [Authorize]
    public Task<IReadOnlyList<CustomFieldDefinitionDto>> GetFormFields(string entityType, CancellationToken ct)
        => Mediator.Send(new GetFormFieldsQuery(entityType), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.CustomFieldsManage)]
    public async Task<IActionResult> Create(CustomFieldDefinitionInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateCustomFieldDefinitionCommand(input), ct);
        return Created($"/api/v1/custom-fields/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.CustomFieldsManage)]
    public Task<CustomFieldDefinitionDto> Update(Guid id, CustomFieldDefinitionInput input, CancellationToken ct)
        => Mediator.Send(new UpdateCustomFieldDefinitionCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(CorePermissions.CustomFieldsManage)]
    public Task<CustomFieldDefinitionDto> Deactivate(Guid id, CancellationToken ct)
        => Mediator.Send(new DeactivateCustomFieldDefinitionCommand(id), ct);

    [HttpGet("values/{entityType}/{id:guid}")]
    [Authorize]
    public Task<JsonObject> GetValues(string entityType, Guid id, CancellationToken ct)
        => Mediator.Send(new GetCustomFieldValuesQuery(entityType, id), ct);

    [HttpPut("values/{entityType}/{id:guid}")]
    [Authorize]
    public Task<JsonObject> SetValues(string entityType, Guid id, JsonObject values, CancellationToken ct)
        => Mediator.Send(new SetCustomFieldValuesCommand(entityType, id, values), ct);
}
