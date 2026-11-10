using ERP.Domain.Catalog;

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

public record PosCatalogItemDto(
    Guid Id, string Sku, string? Barcode, string Name, string Uom, ItemType Type, decimal? Price, decimal TaxRate, decimal? OnHand);

public record PosCatalogPageDto(List<PosCatalogItemDto> Items, int Total);

public record PosCustomerDto(Guid Id, string Code, string Name, string? Phone, string? Email, string? TaxNumber);
