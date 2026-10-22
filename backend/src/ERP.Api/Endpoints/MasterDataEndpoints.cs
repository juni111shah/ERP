using ERP.Application.Catalog;
using ERP.Application.Finance;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
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

        var items = api.MapGroup("/items").WithTags("Master data");
        items.MapPagedList<Item, ItemDto>("", CorePermissions.ItemsRead, "item", ItemDto.From);
        items.MapPost("", async (ItemInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateItem(input), ct);
            return Results.Created($"/api/v1/items/{created.Id}", created);
        }).RequirePermission(CorePermissions.ItemsWrite);
    }

    public static void MapFinance(this RouteGroupBuilder api)
    {
        var finance = api.MapGroup("/finance").WithTags("Finance");
        finance.MapPagedList<Account, AccountDto>("/accounts", CorePermissions.AccountsRead,
            a => new AccountDto(a.Id, a.Code, a.Name, a.Type, a.ReportingGroup, a.SystemRole, a.IsBank, a.IsSystem, a.IsActive));
        finance.MapPagedList<TaxCode, TaxCodeDto>("/tax-codes", CorePermissions.TaxRead,
            t => new TaxCodeDto(t.Id, t.Code, t.Name, t.Kind, t.Rate, t.IsDefault, t.IsRecoverable, t.IsActive));
        finance.MapPagedList<Currency, CurrencyDto>("/currencies", CorePermissions.CurrenciesRead, c => new CurrencyDto(c.Code, c.Name, c.Symbol, c.Decimals));
    }
}
