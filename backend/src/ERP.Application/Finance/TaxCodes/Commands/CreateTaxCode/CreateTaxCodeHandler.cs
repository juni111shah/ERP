using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class CreateTaxCodeHandler(ITaxCodeRepository taxCodes, TaxCodeEditing editing) : ICommandHandler<CreateTaxCodeCommand, TaxCodeDto>
{
    public async Task<TaxCodeDto> Handle(CreateTaxCodeCommand command, CancellationToken ct)
    {
        // 1. The code is unique among the workspace tax codes.
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (await taxCodes.AnyAsync(t => t.Code == code && t.LegalEntityId == null, ct)) throw new ConflictException($"A tax code '{code}' exists.");

        // 2. Apply the input and save.
        var tax = new TaxCode { Code = code };
        await editing.ApplyAsync(tax, command.Input, ct);
        taxCodes.Add(tax);
        await taxCodes.SaveChangesAsync(ct);
        return TaxCodeDto.From(tax);
    }
}
