using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class UpdateTaxCodeHandler(ITaxCodeRepository taxCodes, TaxCodeEditing editing) : ICommandHandler<UpdateTaxCodeCommand, TaxCodeDto>
{
    public async Task<TaxCodeDto> Handle(UpdateTaxCodeCommand command, CancellationToken ct)
    {
        // 1. Load it; a changed code must be free within its scope.
        var tax = await taxCodes.GetForUpdateAsync(command.Id, ct);
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (code != tax.Code && await taxCodes.AnyAsync(t => t.Code == code && t.LegalEntityId == tax.LegalEntityId, ct)) throw new ConflictException($"A tax code '{code}' exists.");

        // 2. Apply the input and save.
        tax.Code = code;
        await editing.ApplyAsync(tax, command.Input, ct);
        await taxCodes.SaveChangesAsync(ct);
        return TaxCodeDto.From(tax);
    }
}
