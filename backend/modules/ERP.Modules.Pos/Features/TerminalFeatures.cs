using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modules.Pos.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

/// <summary>
/// What the till needs to start. These reads ask only for the right to sell, so a cashier does not also need rights to the item,
/// tax code, customer or register screens of the back office.
/// </summary>
public record PosTerminalRegisterDto(
    Guid Id, Guid LegalEntityId, string Code, string Name, Guid? WarehouseId, string? ReceiptHeader, string? ReceiptFooter, Guid? OpenSessionId);

public record PosTerminalSettingsDto(
    decimal MaxDiscountPercent,
    decimal? CashierDiscountPercent,
    bool AllowPartialPayment,
    bool AllowNegativeDrawer,
    bool AllowNegativeStock,
    bool EnforceFloatAtOpen);

public record PosTerminalDto(List<PosTerminalRegisterDto> Registers, PosTerminalSettingsDto Settings);

public record GetPosTerminalQuery : IQuery<PosTerminalDto>;

public record PosCatalogItemDto(
    Guid Id, string Sku, string? Barcode, string Name, string Uom, ItemType Type, decimal? Price, decimal TaxRate, decimal? OnHand);

public record PosCatalogPageDto(List<PosCatalogItemDto> Items, int Total);

/// <summary>Code is an exact look-up (a scan: barcode, then SKU). Search matches part of the name, SKU or barcode.</summary>
public record GetPosCatalogQuery(Guid? RegisterId, string? Search, string? Code, int Page = 1, int PageSize = 60) : IQuery<PosCatalogPageDto>;

public record PosCustomerDto(Guid Id, string Code, string Name, string? Phone, string? Email, string? TaxNumber);

public record GetPosCustomersQuery(string? Search, int Limit = 20) : IQuery<List<PosCustomerDto>>;

public record GetHeldOrdersQuery(Guid? RegisterId) : IQuery<List<PosOrderDto>>;

public class TerminalHandlers(
    IReadRepository<PosRegister> registers,
    IReadRepository<PosSession> sessions,
    IReadRepository<PosOrder> orders,
    IReadRepository<Item> items,
    IReadRepository<TaxCode> taxCodes,
    IReadRepository<Party> parties,
    IStockPostingService stock,
    ISettingsService settings) :
    IRequestHandler<GetPosTerminalQuery, PosTerminalDto>,
    IRequestHandler<GetPosCatalogQuery, PosCatalogPageDto>,
    IRequestHandler<GetPosCustomersQuery, List<PosCustomerDto>>,
    IRequestHandler<GetHeldOrdersQuery, List<PosOrderDto>>
{
    public async Task<PosTerminalDto> Handle(GetPosTerminalQuery request, CancellationToken ct)
    {
        var active = await registers.Query(new Specification<PosRegister>(r => r.IsActive)).OrderBy(r => r.Name).ToListAsync(ct);
        var ids = active.Select(r => r.Id).ToList();
        var open = await sessions.Query(new Specification<PosSession>(s => ids.Contains(s.RegisterId) && s.Status == PosSessionStatus.Open))
            .Select(s => new { s.RegisterId, s.Id }).ToListAsync(ct);
        var rows = active.Select(r => new PosTerminalRegisterDto(r.Id, r.LegalEntityId, r.Code, r.Name, r.WarehouseId, r.ReceiptHeader, r.ReceiptFooter,
            open.FirstOrDefault(s => s.RegisterId == r.Id)?.Id)).ToList();

        var entity = active.FirstOrDefault()?.LegalEntityId;
        return new PosTerminalDto(rows, new PosTerminalSettingsDto(
            await settings.GetAsync<decimal?>("pos.maxDiscountPercent", entity, ct) ?? 100m,
            await settings.GetAsync<decimal?>("pos.cashierDiscountPercent", entity, ct),
            await settings.GetAsync<bool?>("pos.allowPartialPayment", entity, ct) ?? false,
            await settings.GetAsync<bool?>("pos.allowNegativeDrawer", entity, ct) ?? false,
            await settings.GetAsync<bool?>("pos.allowNegativeStock", entity, ct) ?? true,
            await settings.GetAsync<bool?>("pos.enforceFloatAtOpen", entity, ct) ?? true));
    }

    public async Task<PosCatalogPageDto> Handle(GetPosCatalogQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var page = Math.Max(request.Page, 1);
        var code = request.Code?.Trim();
        var search = request.Search?.Trim();

        var query = items.Query(new Specification<Item>(i => i.IsActive));
        if (!string.IsNullOrEmpty(code))
            query = query.Where(i => i.Barcode == code || i.Sku == code);
        else if (!string.IsNullOrEmpty(search))
            query = query.Where(i => i.Name.Contains(search) || i.Sku.Contains(search) || (i.Barcode != null && i.Barcode.Contains(search)));

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(i => i.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var taxIds = rows.Where(i => i.TaxCodeId.HasValue).Select(i => i.TaxCodeId!.Value).Distinct().ToList();
        var rates = taxIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await taxCodes.Query(new Specification<TaxCode>(t => taxIds.Contains(t.Id))).Select(t => new { t.Id, t.Rate }).ToDictionaryAsync(t => t.Id, t => t.Rate, ct);

        var onHand = (IReadOnlyDictionary<Guid, decimal>)new Dictionary<Guid, decimal>();
        if (request.RegisterId is { } registerId && await registers.FindAsync(registerId, ct) is { } register)
            onHand = await stock.GetOnHandAsync(rows.Select(i => i.Id).ToList(), register.WarehouseId, register.LegalEntityId, ct);

        return new PosCatalogPageDto(rows.Select(i => new PosCatalogItemDto(
            i.Id, i.Sku, i.Barcode, i.Name, i.Uom, i.Type, i.SalesPrice,
            i.TaxCodeId is { } t && rates.TryGetValue(t, out var rate) ? rate : 0m,
            onHand.TryGetValue(i.Id, out var q) ? q : null)).ToList(), total);
    }

    public async Task<List<PosCustomerDto>> Handle(GetPosCustomersQuery request, CancellationToken ct)
    {
        var search = request.Search?.Trim();
        var query = parties.Query(new Specification<Party>(p => p.Roles == PartyRoles.None || (p.Roles & PartyRoles.Customer) == PartyRoles.Customer));
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search) || p.Code.Contains(search) || (p.Phone != null && p.Phone.Contains(search)) || (p.TaxNumber != null && p.TaxNumber.Contains(search)));
        return await query.OrderBy(p => p.Name).Take(Math.Clamp(request.Limit, 1, 50))
            .Select(p => new PosCustomerDto(p.Id, p.Code, p.Name, p.Phone, p.Email, p.TaxNumber)).ToListAsync(ct);
    }

    public async Task<List<PosOrderDto>> Handle(GetHeldOrdersQuery request, CancellationToken ct)
    {
        var registerId = request.RegisterId;
        var held = await orders.Query(new Specification<PosOrder>(o => o.Status == PosOrderStatus.Parked && (registerId == null || o.RegisterId == registerId)))
            .Include(o => o.Lines).Include(o => o.Payments).OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync(ct);
        return held.Select(OrderHandlers.Map).ToList();
    }
}
