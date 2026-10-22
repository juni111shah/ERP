using ERP.Domain.Common;

namespace ERP.Domain.Organizations;

/// <summary>An organization (tenant). Everything else hangs off it.</summary>
public class Organization : Entity, IAuditable
{
    public string Name { get; set; } = default!;
    /// <summary>Sign-in key / org code, unique across the platform (e.g. "demo").</summary>
    public string Code { get; set; } = default!;
    public string IndustryCode { get; set; } = "generic";
    public string CountryCode { get; set; } = default!;
    /// <summary>Required; chosen at onboarding (a country pack can suggest it).</summary>
    public string BaseCurrency { get; set; } = default!;
    public string TimeZone { get; set; } = "UTC";
    public string DefaultLocale { get; set; } = "en";
    public string Plan { get; set; } = "standard";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>A company/legal entity inside an organization. The accounting boundary (journals, tax registration, period lock).</summary>
public class LegalEntity : OrgEntity, IHasCustomFields
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? LegalName { get; set; }
    public string CountryCode { get; set; } = default!;
    public string BaseCurrency { get; set; } = default!;
    public string? TaxRegistrationNo { get; set; }
    public bool IsTaxRegistered { get; set; }
    public string? RegistrationNo { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? BooksLockedTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CustomFields { get; set; }
}

/// <summary>Business unit (branch, store, site, department) of a legal entity.</summary>
public class BusinessUnit : LegalEntityScopedEntity, IHasCustomFields
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CustomFields { get; set; }
}

/// <summary>Per-tenant module enablement. Disabling never deletes module data.</summary>
public class OrganizationModule : OrgEntity
{
    public string ModuleKey { get; set; } = default!;
    public bool IsEnabled { get; set; }
    public DateTime? EnabledAt { get; set; }
    public DateTime? DisabledAt { get; set; }
}
