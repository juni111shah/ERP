using ERP.Domain.Common;

namespace ERP.Domain.Parties;

public enum PartyKind { Individual, Company }

[Flags]
public enum PartyRoles { None = 0, Customer = 1, Vendor = 2, Employee = 4, Other = 8 }

/// <summary>Customers, vendors and other counterparties share one table (role flags). Shared across legal entities of a tenant.</summary>
public class Party : OrgEntity, IHasCustomFields
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public PartyKind Kind { get; set; }
    public PartyRoles Roles { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? TaxNumber { get; set; }
    /// <summary>Sensitive: national id / passport. Masked without sensitive.read.</summary>
    public string? IdNumber { get; set; }
    /// <summary>Sensitive: bank account / IBAN. Masked without sensitive.read.</summary>
    public string? Iban { get; set; }
    public string? CurrencyCode { get; set; }
    public int? PaymentTermsDays { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CustomFields { get; set; }
}
