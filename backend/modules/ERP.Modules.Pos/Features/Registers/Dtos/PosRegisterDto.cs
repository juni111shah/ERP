using System.Linq.Expressions;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

public record PosRegisterDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid? WarehouseId,
    Guid? DefaultCashAccountId,
    bool IsActive,
    string? ReceiptHeader,
    string? ReceiptFooter,
    string? CustomFields,
    DateTimeOffset CreatedAt,
    Guid? CardAccountId = null,
    Guid? VarianceAccountId = null,
    Guid? ExpenseAccountId = null)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<PosRegister, PosRegisterDto>> Projection = r => new PosRegisterDto(
        r.Id, r.LegalEntityId, r.Code, r.Name, r.WarehouseId, r.DefaultCashAccountId,
        r.IsActive, r.ReceiptHeader, r.ReceiptFooter, r.CustomFields, r.CreatedAt, r.CardAccountId, r.VarianceAccountId, r.ExpenseAccountId);

    public static PosRegisterDto From(PosRegister r) => new(
        r.Id,
        r.LegalEntityId,
        r.Code,
        r.Name,
        r.WarehouseId,
        r.DefaultCashAccountId,
        r.IsActive,
        r.ReceiptHeader,
        r.ReceiptFooter,
        r.CustomFields,
        r.CreatedAt,
        r.CardAccountId,
        r.VarianceAccountId,
        r.ExpenseAccountId);
}
