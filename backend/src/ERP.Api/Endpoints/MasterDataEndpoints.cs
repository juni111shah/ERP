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
        items.MapGet("/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetItem(id), ct)).RequirePermission(CorePermissions.ItemsRead);
        items.MapPut("/{id:guid}", (Guid id, ItemInput input, ISender sender, CancellationToken ct) => sender.Send(new UpdateItem(id, input), ct)).RequirePermission(CorePermissions.ItemsWrite);
        items.MapPost("", async (ItemInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateItem(input), ct);
            return Results.Created($"/api/v1/items/{created.Id}", created);
        }).RequirePermission(CorePermissions.ItemsWrite);
    }

    public static void MapFinance(this RouteGroupBuilder api)
    {
        var finance = api.MapGroup("/finance").WithTags("Finance");
        finance.MapPagedList<Account, AccountDto>("/accounts", CorePermissions.AccountsRead, "account", AccountDto.From);
        finance.MapPost("/accounts", async (AccountInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateAccount(input), ct);
            return Results.Created($"/api/v1/finance/accounts/{created.Id}", created);
        }).RequirePermission(CorePermissions.AccountsWrite);
        finance.MapGet("/accounts/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetAccount(id), ct)).RequirePermission(CorePermissions.AccountsRead);
        finance.MapPut("/accounts/{id:guid}", (Guid id, AccountInput input, ISender sender, CancellationToken ct) => sender.Send(new UpdateAccount(id, input), ct)).RequirePermission(CorePermissions.AccountsWrite);

        // Step-by-step chart of accounts: one level per request (types -> sub-categories -> primary accounts -> children), each with hasChildren.
        finance.MapGet("/accounts/tree/categories", ([AsParameters] GetAccountCategories query, ISender sender, CancellationToken ct) => sender.Send(query, ct)).RequirePermission(CorePermissions.AccountsRead);
        finance.MapGet("/accounts/tree/categories/{type}/accounts", ([AsParameters] ListCategoryAccounts query, ISender sender, CancellationToken ct) => sender.Send(query, ct)).RequirePermission(CorePermissions.AccountsRead);
        finance.MapGet("/accounts/tree/categories/{type}/subcategories", ([AsParameters] GetAccountSubcategories query, ISender sender, CancellationToken ct) => sender.Send(query, ct)).RequirePermission(CorePermissions.AccountsRead);
        finance.MapGet("/accounts/tree/categories/{type}/subcategories/{key}/accounts", ([AsParameters] ListSubcategoryAccounts query, ISender sender, CancellationToken ct) => sender.Send(query, ct)).RequirePermission(CorePermissions.AccountsRead);
        finance.MapGet("/accounts/{id:guid}/children", ([AsParameters] ListAccountChildren query, ISender sender, CancellationToken ct) => sender.Send(query, ct)).RequirePermission(CorePermissions.AccountsRead);
        finance.MapGet("/accounts/{id:guid}/path", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetAccountPath(id), ct)).RequirePermission(CorePermissions.AccountsRead);

        finance.MapPagedList<TaxCode, TaxCodeDto>("/tax-codes", CorePermissions.TaxRead,
            t => new TaxCodeDto(t.Id, t.Code, t.Name, t.Kind, t.Rate, t.IsDefault, t.IsRecoverable, t.IsActive));
        finance.MapPost("/tax-codes", async (TaxCodeInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateTaxCode(input), ct);
            return Results.Created($"/api/v1/finance/tax-codes/{created.Id}", created);
        }).RequirePermission(CorePermissions.TaxWrite);
        finance.MapPut("/tax-codes/{id:guid}", (Guid id, TaxCodeInput input, ISender sender, CancellationToken ct) => sender.Send(new UpdateTaxCode(id, input), ct)).RequirePermission(CorePermissions.TaxWrite);
        finance.MapPagedList<Currency, CurrencyDto>("/currencies", CorePermissions.CurrenciesRead, c => new CurrencyDto(c.Code, c.Name, c.Symbol, c.Decimals));
    }
}
