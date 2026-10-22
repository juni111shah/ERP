using System.Text.Json;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Application.Settings;
using ERP.Domain.Organizations;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganization(this RouteGroupBuilder api)
    {
        var entities = api.MapGroup("/legal-entities").WithTags("Organization");
        entities.MapPagedList<LegalEntity, LegalEntityDto>("", CorePermissions.EntitiesRead, "legalentity", LegalEntityDto.From);
        entities.MapPost("", async (LegalEntityInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateLegalEntity(input), ct);
            return Results.Created($"/api/v1/legal-entities/{created.Id}", created);
        }).RequirePermission(CorePermissions.EntitiesWrite);
        entities.MapPut("/{id:guid}", (Guid id, LegalEntityInput input, ISender sender, CancellationToken ct) =>
            sender.Send(new UpdateLegalEntity(id, input), ct)).RequirePermission(CorePermissions.EntitiesWrite);

        var units = api.MapGroup("/business-units").WithTags("Organization");
        units.MapPagedList<BusinessUnit, BusinessUnitDto>("", CorePermissions.BusinessUnitsRead, "businessunit", BusinessUnitDto.From);
        units.MapPost("", async (BusinessUnitInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateBusinessUnit(input), ct);
            return Results.Created($"/api/v1/business-units/{created.Id}", created);
        }).RequirePermission(CorePermissions.BusinessUnitsWrite);

        units.MapPut("/{id:guid}", (Guid id, BusinessUnitInput input, ISender sender, CancellationToken ct) => sender.Send(new UpdateBusinessUnit(id, input), ct)).RequirePermission(CorePermissions.BusinessUnitsWrite);

        var settings = api.MapGroup("/settings").WithTags("Organization");
        settings.MapGet("", (ISender sender, CancellationToken ct) => sender.Send(new ListSettings(), ct)).RequirePermission(CorePermissions.SettingsRead);
        settings.MapPut("", async (Dictionary<string, JsonElement> values, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateSettings(values), ct);
            return Results.NoContent();
        }).RequireAuthorization(); // per-key permission is checked by the handler
    }
}
