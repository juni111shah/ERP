using System.Text.Json;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;

namespace ERP.Application.Finance;

/// <summary>
/// Checks an invoice that carries tax against what the legal entity's country pack expects on a tax invoice. The result is a list of
/// warnings shown when the invoice is issued; nothing is ever blocked.
/// </summary>
public static class InvoiceRequirements
{
    private static readonly string[] TaxNumberKeys = ["trn", "vat_no", "gstin", "ntn", "strn", "tax_no", "ein"];

    public static IReadOnlyList<string> Warnings(ICountryPack? pack, LegalEntity entity, Party? customer, Invoice invoice)
    {
        var warnings = new List<string>();
        if (pack is null || invoice.Tax == 0) return warnings;
        foreach (var field in pack.InvoiceRequiredFields)
        {
            if (field == InvoiceField.SellerTaxNumber && string.IsNullOrWhiteSpace(entity.TaxRegistrationNo))
                warnings.Add($"The legal entity has no tax registration number. {pack.Name} expects it on a tax invoice.");
            if (field == InvoiceField.CustomerTaxNumber && customer is not null && !HasTaxNumber(customer))
                warnings.Add($"The customer has no tax registration number. {pack.Name} expects it on business-to-business tax invoices.");
        }
        return warnings;
    }

    /// <summary>The core TaxNumber, or any custom field whose key names a tax number (a country pack adds fields such as cf_ae_trn).</summary>
    private static bool HasTaxNumber(Party p)
    {
        if (!string.IsNullOrWhiteSpace(p.TaxNumber)) return true;
        if (string.IsNullOrWhiteSpace(p.CustomFields)) return false;
        try
        {
            using var doc = JsonDocument.Parse(p.CustomFields);
            return doc.RootElement.EnumerateObject().Any(f => TaxNumberKeys.Any(k => f.Name.Contains(k, StringComparison.OrdinalIgnoreCase))
                && f.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(f.Value.GetString()));
        }
        catch (JsonException) { return false; }
    }
}
