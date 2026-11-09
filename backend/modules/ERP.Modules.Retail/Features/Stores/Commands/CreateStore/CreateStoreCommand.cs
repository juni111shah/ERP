using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>Opens a retail store, optionally tied to a warehouse.</summary>
public record CreateStoreCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid? WarehouseId,
    string? StoreManagerName,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    string? CustomFields) : ICommand<RetailStoreDto>;
