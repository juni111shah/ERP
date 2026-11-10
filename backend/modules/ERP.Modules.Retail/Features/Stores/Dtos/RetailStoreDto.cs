using System.Linq.Expressions;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

public record RetailStoreDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid? WarehouseId,
    string? StoreManagerName,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<RetailStore, RetailStoreDto>> Projection = s => new RetailStoreDto(
        s.Id, s.LegalEntityId, s.Code, s.Name, s.WarehouseId, s.StoreManagerName,
        s.Phone, s.Email, s.Address, s.IsActive, s.CustomFields, s.CreatedAt);

    public static RetailStoreDto From(RetailStore s) => new(
        s.Id,
        s.LegalEntityId,
        s.Code,
        s.Name,
        s.WarehouseId,
        s.StoreManagerName,
        s.Phone,
        s.Email,
        s.Address,
        s.IsActive,
        s.CustomFields,
        s.CreatedAt);
}
