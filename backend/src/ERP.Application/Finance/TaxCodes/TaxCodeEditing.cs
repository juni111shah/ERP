using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Applies a tax code's input, shared by create and update.</summary>
public sealed class TaxCodeEditing(ITaxCodeRepository taxCodes)
{
    /// <summary>Applies the input; making a code the default clears the default flag on every other tax code.</summary>
    public async Task ApplyAsync(TaxCode tax, TaxCodeInput i, CancellationToken ct)
    {
        tax.Name = i.Name.Trim(); tax.Kind = i.Kind; tax.Rate = i.Rate; tax.IsRecoverable = i.IsRecoverable; tax.IsActive = i.IsActive;
        if (i.IsDefault && !tax.IsDefault)
            foreach (var other in await taxCodes.ListOtherDefaultsForUpdateAsync(tax.Id, ct)) other.IsDefault = false;
        tax.IsDefault = i.IsDefault;
    }
}
