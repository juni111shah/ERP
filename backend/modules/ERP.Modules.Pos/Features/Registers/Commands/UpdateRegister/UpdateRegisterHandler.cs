using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

internal sealed class UpdateRegisterHandler(IRepository<PosRegister> registers, PosRegisterSetup setup) : ICommandHandler<UpdateRegisterCommand, PosRegisterDto>
{
    public async Task<PosRegisterDto> Handle(UpdateRegisterCommand request, CancellationToken cancellationToken)
    {
        // 1. A register with an open shift cannot be deactivated; the warehouse and accounts must be usable.
        var register = await registers.GetForUpdateAsync(request.Id, cancellationToken);
        if (!request.IsActive && register.IsActive) await setup.EnsureNoOpenSessionAsync(register.Id, "deactivate", cancellationToken);
        await setup.EnsureSetupAsync(register.LegalEntityId, request.WarehouseId, request.DefaultCashAccountId, request.CardAccountId, request.VarianceAccountId, request.ExpenseAccountId, cancellationToken);

        // 2. Replace the record (the card, variance and expense accounts only when sent).
        register.Name = request.Name.Trim();
        register.WarehouseId = request.WarehouseId;
        register.DefaultCashAccountId = request.DefaultCashAccountId;
        register.CardAccountId = PosRegisterSetup.Merge(request.CardAccountId, register.CardAccountId);
        register.VarianceAccountId = PosRegisterSetup.Merge(request.VarianceAccountId, register.VarianceAccountId);
        register.ExpenseAccountId = PosRegisterSetup.Merge(request.ExpenseAccountId, register.ExpenseAccountId);
        register.IsActive = request.IsActive;
        register.ReceiptHeader = request.ReceiptHeader;
        register.ReceiptFooter = request.ReceiptFooter;
        register.CustomFields = request.CustomFields;

        await registers.SaveChangesAsync(cancellationToken);
        return PosRegisterDto.From(register);
    }
}
