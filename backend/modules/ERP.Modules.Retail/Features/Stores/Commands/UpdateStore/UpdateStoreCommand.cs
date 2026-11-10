using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>Updates a retail store.</summary>
public record UpdateStoreCommand(
    Guid Id,
    string Name,
    Guid? WarehouseId,
    string? StoreManagerName,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    string? CustomFields) : ICommand<RetailStoreDto>;
