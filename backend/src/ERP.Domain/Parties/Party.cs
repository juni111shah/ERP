using ERP.Domain.Common;

namespace ERP.Domain.Parties;

public enum PartyKind { Individual, Company }

[Flags]
public enum PartyRoles { None = 0, Customer = 1, Vendor = 2, Employee = 4, Other = 8 }

/// <summary>Customers, vendors and other counterparties share one table (role flags). Shared across legal entities of a tenant.</summary>
[DefaultSort("code")]
public class Party : OrgEntity, IHasCustomFields
{
    [Listable]
    public string Code { get; set; } = default!;
    [Listable]
    public string Name { get; set; } = default!;
    [Filterable]
    public PartyKind Kind { get; set; }
    [Filterable]
    public PartyRoles Roles { get; set; }
    [Searchable, Sortable]
    public string? Email { get; set; }
    [Searchable]
    public string? Phone { get; set; }
    [Searchable]
    public string? TaxNumber { get; set; }
    /// <summary>Sensitive: national id / passport. Masked without sensitive.read.</summary>
    public string? IdNumber { get; set; }
    /// <summary>Sensitive: bank account / IBAN. Masked without sensitive.read.</summary>
    public string? Iban { get; set; }
    [Filterable]
    public string? CurrencyCode { get; set; }
    public int? PaymentTermsDays { get; set; }
    public string? Address { get; set; }
    [Filterable, Sortable]
    public bool IsActive { get; set; } = true;
    public string? CustomFields { get; set; }
}
