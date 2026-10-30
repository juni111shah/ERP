using ERP.Application.Abstractions;
using ERP.Application.Catalog;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Parties;
using MediatR;

namespace ERP.Application.Imports;

/// <summary>Customers, vendors and other parties from CSV.</summary>
public sealed class PartyImport(ISender sender) : IImportDefinition
{
    public string Key => "parties";
    public string Label => "Parties (customers, vendors)";
    public string? ModuleKey => null;
    public string Permission => CorePermissions.PartiesWrite;
    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new("name", true, "Al Noor Trading LLC"), new("kind", false, "Company", "Individual or Company (default Company)"), new("roles", false, "Customer", "Customer, Vendor, Employee, Other - separate several with ;"),
        new("email", false, "accounts@alnoor.example"), new("phone", false, "+971500000001"), new("taxNumber", false, "100123456700003"), new("idNumber", false, ""),
        new("iban", false, ""), new("currencyCode", false, "QAR"), new("paymentTermsDays", false, "30"), new("address", false, "West Bay, Doha"),
    ];

    public async Task ImportRowAsync(ImportRow r, CancellationToken ct)
    {
        var roles = r.Get("roles") is { } v ? (Enum.TryParse<PartyRoles>(v.Replace(';', ',').Replace('|', ','), true, out var parsed) ? parsed : throw new ImportRowException("'roles' must be Customer, Vendor, Employee or Other.")) : PartyRoles.Customer;
        await sender.Send(new CreateParty(new PartyInput(r.Require("name"), r.Enum<PartyKind>("kind", PartyKind.Company), roles, r.Get("email"), r.Get("phone"), r.Get("taxNumber"), r.Get("idNumber"),
            r.Get("iban"), r.Get("currencyCode"), r.Int("paymentTermsDays"), r.Get("address"))), ct);
    }
}

/// <summary>Products and services from CSV.</summary>
public sealed class ItemImport(ISender sender) : IImportDefinition
{
    public string Key => "items";
    public string Label => "Items (products, services)";
    public string? ModuleKey => null;
    public string Permission => CorePermissions.ItemsWrite;
    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new("sku", true, "SRV-MAINT"), new("name", true, "Maintenance call-out"), new("type", true, "Service", "Product or Service"), new("uom", true, "visit", "Unit of measure"),
        new("salesPrice", false, "250"), new("purchasePrice", false, "150"),
    ];

    public async Task ImportRowAsync(ImportRow r, CancellationToken ct)
        => await sender.Send(new CreateItem(new ItemInput(r.Require("sku"), r.Require("name"), r.Enum<ItemType>("type"), r.Require("uom"), r.Decimal("salesPrice"), r.Decimal("purchasePrice"), null)), ct);
}
