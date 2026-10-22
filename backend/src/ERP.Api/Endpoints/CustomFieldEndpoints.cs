using ERP.Modularity;
using System.Text.Json.Nodes;
using ERP.Application.Customization;
using ERP.Application.Security;
using MediatR;

namespace ERP.Api.Endpoints;

public static class CustomFieldEndpoints
{
    public static void MapCustomFields(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/custom-fields").WithTags("Customization");
        g.MapGet("/entity-types", (ISender sender, CancellationToken ct) => sender.Send(new ListCustomizableEntities(), ct)).RequirePermission(CorePermissions.CustomFieldsRead);
        g.MapGet("", (string entityType, bool? includeInactive, ISender sender, CancellationToken ct) =>
            sender.Send(new ListCustomFieldDefinitions(entityType, includeInactive ?? false), ct)).RequirePermission(CorePermissions.CustomFieldsRead);
        // Form metadata: active definitions for rendering, readable by anyone who can read the entity type (checked by the handler).
        g.MapGet("/form/{entityType}", (string entityType, ISender sender, CancellationToken ct) => sender.Send(new GetFormFields(entityType), ct)).RequireAuthorization();
        g.MapPost("", async (CustomFieldDefinitionInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateCustomFieldDefinition(input), ct);
            return Results.Created($"/api/v1/custom-fields/{created.Id}", created);
        }).RequirePermission(CorePermissions.CustomFieldsManage);
        g.MapPut("/{id:guid}", (Guid id, CustomFieldDefinitionInput input, ISender sender, CancellationToken ct) =>
            sender.Send(new UpdateCustomFieldDefinition(id, input), ct)).RequirePermission(CorePermissions.CustomFieldsManage);
        g.MapDelete("/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new DeactivateCustomFieldDefinition(id), ct)).RequirePermission(CorePermissions.CustomFieldsManage);
        g.MapGet("/values/{entityType}/{id:guid}", (string entityType, Guid id, ISender sender, CancellationToken ct) =>
            sender.Send(new GetCustomFieldValues(entityType, id), ct)).RequireAuthorization();
        g.MapPut("/values/{entityType}/{id:guid}", (string entityType, Guid id, JsonObject values, ISender sender, CancellationToken ct) =>
            sender.Send(new SetCustomFieldValues(entityType, id, values), ct)).RequireAuthorization();
    }
}
