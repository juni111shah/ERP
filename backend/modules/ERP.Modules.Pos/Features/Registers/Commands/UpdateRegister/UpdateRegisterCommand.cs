using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>
/// Updates a cash register. The update replaces the whole record, except the card, variance and expense accounts: left out they keep
/// their value, because older clients do not send them, and an empty GUID clears them.
/// </summary>
public record UpdateRegisterCommand(
    Guid Id,
    string Name,
    Guid? WarehouseId,
    Guid? DefaultCashAccountId,
    bool IsActive,
    string? ReceiptHeader,
    string? ReceiptFooter,
    string? CustomFields,
    Guid? CardAccountId = null,
    Guid? VarianceAccountId = null,
    Guid? ExpenseAccountId = null) : ICommand<PosRegisterDto>;
