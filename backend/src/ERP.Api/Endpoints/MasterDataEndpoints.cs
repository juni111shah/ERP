using ERP.Application.Catalog;
using ERP.Application.Finance;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;


public static class MasterDataEndpoints
{
    public static void MapMasterData(this RouteGroupBuilder api)
    {
        var parties = api.MapGroup("/parties").WithTags("Master data");
        parties.MapGet("", ([AsParameters] ListParties query, ISender sender, CancellationToken ct) => sender.Send(query, ct)).RequirePermission(CorePermissions.PartiesRead);
        parties.MapGet("/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetParty(id), ct)).RequirePermission(CorePermissions.PartiesRead);
        parties.MapPost("", async (PartyInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateParty(input), ct);
            return Results.Created($"/api/v1/parties/{created.Id}", created);
        }).RequirePermission(CorePermissions.PartiesWrite);
        parties.MapPut("/{id:guid}", (Guid id, PartyInput input, ISender sender, CancellationToken ct) => sender.Send(new UpdateParty(id, input), ct)).RequirePermission(CorePermissions.PartiesWrite);

        var groups = api.MapGroup("/party-groups").WithTags("Master data");
        groups.MapPagedList<PartyGroup, PartyGroupDto>("", CorePermissions.PartyGroupsRead, g => new PartyGroupDto(g.Id, g.Code, g.Name, g.IsActive));
        groups.MapPost("", async (PartyGroupInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreatePartyGroup(input), ct);
            return Results.Created($"/api/v1/party-groups/{created.Id}", created);
        }).RequirePermission(CorePermissions.PartyGroupsWrite);
        groups.MapPut("/{id:guid}", (Guid id, PartyGroupInput input, ISender sender, CancellationToken ct) => sender.Send(new UpdatePartyGroup(id, input), ct)).RequirePermission(CorePermissions.PartyGroupsWrite);
        groups.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => { await sender.Send(new DeletePartyGroup(id), ct); return Results.NoContent(); }).RequirePermission(CorePermissions.PartyGroupsWrite);

        var items = api.MapGroup("/items").WithTags("Master data");
        items.MapPagedList<Item, ItemDto>("", CorePermissions.ItemsRead, "item", ItemDto.From);
        items.MapGet("/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetItem(id), ct)).RequirePermission(CorePermissions.ItemsRead);
        items.MapPut("/{id:guid}", (Guid id, ItemInput input, ISender sender, CancellationToken ct) => sender.Send(new UpdateItem(id, input), ct)).RequirePermission(CorePermissions.ItemsWrite);
        items.MapPost("", async (ItemInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateItem(input), ct);
            return Results.Created($"/api/v1/items/{created.Id}", created);
        }).RequirePermission(CorePermissions.ItemsWrite);
    }
}
