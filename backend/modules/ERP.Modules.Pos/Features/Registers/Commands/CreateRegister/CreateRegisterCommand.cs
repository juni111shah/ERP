using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Creates a cash register.</summary>
public record CreateRegisterCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid? WarehouseId,
    Guid? DefaultCashAccountId,
    bool IsActive = true,
    string? ReceiptHeader = null,
    string? ReceiptFooter = null,
    string? CustomFields = null,
    Guid? CardAccountId = null,
    Guid? VarianceAccountId = null,
    Guid? ExpenseAccountId = null) : ICommand<PosRegisterDto>;
