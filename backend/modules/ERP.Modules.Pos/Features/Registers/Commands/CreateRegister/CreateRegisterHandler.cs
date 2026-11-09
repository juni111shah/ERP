using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

internal sealed class CreateRegisterHandler(IRepository<PosRegister> registers, ILegalEntityGuard guard, PosRegisterSetup setup)
    : ICommandHandler<CreateRegisterCommand, PosRegisterDto>
{
    public async Task<PosRegisterDto> Handle(CreateRegisterCommand request, CancellationToken cancellationToken)
    {
        // 1. The caller must work in the legal entity, and the code must be free.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var code = request.Code.Trim();
        if (await registers.AnyAsync(r => r.Code == code, cancellationToken)) throw new ConflictException($"A register with code {code} already exists.");

        // 2. The warehouse and accounts must be usable.
        await setup.EnsureSetupAsync(request.LegalEntityId, request.WarehouseId, request.DefaultCashAccountId, request.CardAccountId, request.VarianceAccountId, request.ExpenseAccountId, cancellationToken);

        // 3. Save.
        var register = new PosRegister
        {
            LegalEntityId = request.LegalEntityId,
            Code = code,
            Name = request.Name.Trim(),
            WarehouseId = request.WarehouseId,
            DefaultCashAccountId = request.DefaultCashAccountId,
            CardAccountId = PosRegisterSetup.Merge(request.CardAccountId, null),
            VarianceAccountId = PosRegisterSetup.Merge(request.VarianceAccountId, null),
            ExpenseAccountId = PosRegisterSetup.Merge(request.ExpenseAccountId, null),
            IsActive = request.IsActive,
            ReceiptHeader = request.ReceiptHeader,
            ReceiptFooter = request.ReceiptFooter,
            CustomFields = request.CustomFields
        };

        registers.Add(register);
        await registers.SaveChangesAsync(cancellationToken);
        return PosRegisterDto.From(register);
    }
}
