using ERP.Domain.Common;

namespace ERP.Domain.Platform;

// Platform-level records: they belong to the company that runs this ERP, not to one workspace, so none of them
// is org-scoped. Every read by a workspace user must filter on the caller's own OrgId explicitly.

public enum SubscriptionStatus { Trialing, Active, PastDue, Expired, Suspended, Cancelled }
public enum BillingCycle { Monthly, Annual }
public enum SubscriptionInvoiceStatus { Open, Paid, Void }
public enum SignupStatus { PendingEmail, PendingReview, Provisioned, Rejected, Expired }
public enum SignupMode { Open, Review, Closed }

/// <summary>What a workspace may do right now, worked out from its subscription.</summary>
public enum SubscriptionAccessLevel { Full, ReadOnly, Blocked }

/// <summary>A price list entry the founder sells: price per cycle, trial length and limits.</summary>
public class SubscriptionPlan : Entity, IAuditable
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    /// <summary>Shown on the public registration page. Private plans are assigned by the founder only.</summary>
    public bool IsPublic { get; set; } = true;
    /// <summary>Archived plans keep their existing clients but cannot be chosen again.</summary>
    public bool IsActive { get; set; } = true;
    public bool IsRecommended { get; set; }
    public int SortOrder { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int TrialDays { get; set; }
    /// <summary>Null means no limit.</summary>
    public int? MaxUsers { get; set; }
    /// <summary>Null means no limit.</summary>
    public int? MaxLegalEntities { get; set; }
    /// <summary>Comma-separated module keys the plan includes. Empty means every installed module.</summary>
    public string? ModuleKeys { get; set; }
    /// <summary>Selling points for the pricing cards, one per line.</summary>
    public string? Features { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public IReadOnlyList<string> ModuleList() => SplitList(ModuleKeys, ',');
    public IReadOnlyList<string> FeatureList() => SplitList(Features, '\n');
    private static List<string> SplitList(string? value, char separator)
        => (value ?? "").Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

/// <summary>One workspace's plan and billing state. A workspace with no row (internal or created before billing existed) is unrestricted.</summary>
public class WorkspaceSubscription : Entity, IAuditable
{
    public Guid OrgId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public BillingCycle Cycle { get; set; }
    public DateOnly StartedOn { get; set; }
    public DateOnly? TrialEndsOn { get; set; }
    public DateOnly? CurrentPeriodStart { get; set; }
    public DateOnly? CurrentPeriodEnd { get; set; }
    /// <summary>A negotiated price for one cycle, in place of the plan's list price.</summary>
    public decimal? CustomPrice { get; set; }
    /// <summary>Free of charge: never invoiced and never lapses.</summary>
    public bool IsComplimentary { get; set; }
    /// <summary>The day the trial ran out or the first invoice went overdue; grace and suspension are counted from it.</summary>
    public DateOnly? LapsedOn { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? SuspendedReason { get; set; }
    /// <summary>True when the founder suspended by hand: a payment alone then does not lift it.</summary>
    public bool SuspendedByFounder { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public string? BillingEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Notes { get; set; }
    /// <summary>A plan change the client asked for, waiting for the founder.</summary>
    public Guid? RequestedPlanId { get; set; }
    public BillingCycle? RequestedCycle { get; set; }
    public string? RequestNote { get; set; }
    public DateTime? RequestedAt { get; set; }
    /// <summary>Stops the same reminder going out twice.</summary>
    public string? LastReminderKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>An invoice from the founder's company to a client for the use of the ERP. Nothing here touches the client's own ledger.</summary>
public class SubscriptionInvoice : Entity, IAuditable
{
    public string Number { get; set; } = default!;
    public Guid OrgId { get; set; }
    public Guid SubscriptionId { get; set; }
    public SubscriptionInvoiceStatus Status { get; set; }
    public string Description { get; set; } = default!;
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxPercent { get; set; }
    /// <summary>The name of the tax when issued, so an old invoice still prints what it said.</summary>
    public string TaxLabel { get; set; } = "Tax";
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = default!;
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public string? VoidReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>A registration from the public page. Holds a password hash only, never the password.</summary>
public class SignupRequest : Entity, IAuditable
{
    public SignupStatus Status { get; set; }
    public string BusinessName { get; set; } = default!;
    public string SignInKey { get; set; } = default!;
    public string Industry { get; set; } = default!;
    public string CountryCode { get; set; } = default!;
    public string BaseCurrency { get; set; } = default!;
    public string? TimeZone { get; set; }
    public string ContactName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    /// <summary>Null when the email already has an account and its password was confirmed at registration.</summary>
    public string? PasswordHash { get; set; }
    public Guid PlanId { get; set; }
    public BillingCycle Cycle { get; set; }
    public string? VerificationTokenHash { get; set; }
    public DateTime? VerificationExpiresAt { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? RejectReason { get; set; }
    public Guid? ProvisionedOrgId { get; set; }
    /// <summary>When the person ticked "I accept". Registration is refused without it.</summary>
    public DateTime? TermsAcceptedAt { get; set; }
    public string? CreatedIp { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>The founder's settings for registration and billing. There is one row.</summary>
public class PlatformSetting : Entity, IAuditable
{
    public SignupMode SignupMode { get; set; } = SignupMode.Review;
    /// <summary>Ask new registrations to confirm their email. Without an email server they go to the founder's queue instead.</summary>
    public bool RequireEmailVerification { get; set; } = true;
    public Guid? DefaultPlanId { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal TaxPercent { get; set; }
    public string TaxLabel { get; set; } = "Tax";
    public string InvoicePrefix { get; set; } = "SUB";
    public int NextInvoiceNumber { get; set; } = 1;
    /// <summary>Days a manually issued invoice has until it is due.</summary>
    public int InvoiceDueDays { get; set; } = 7;
    /// <summary>How long before a period ends its renewal invoice is issued.</summary>
    public int RenewalLeadDays { get; set; } = 7;
    /// <summary>Days of normal use after an invoice goes overdue, before the workspace turns read-only.</summary>
    public int GraceDays { get; set; } = 7;
    /// <summary>Days after lapsing (trial ended or invoice overdue) before the workspace is blocked.</summary>
    public int SuspendAfterDays { get; set; } = 30;
    public string CompanyName { get; set; } = "";
    public string? CompanyAddress { get; set; }
    public string? CompanyTaxNumber { get; set; }
    public string? SupportEmail { get; set; }
    /// <summary>Terms kept on another site: the links go there instead of the page below.</summary>
    public string? TermsUrl { get; set; }
    /// <summary>The founder's wording. Null means the starter wording.</summary>
    public string? TermsText { get; set; }
    public string? PrivacyText { get; set; }
    public DateTime? LegalUpdatedAt { get; set; }
    /// <summary>Bank details and how to pay; shown to clients and printed on invoices.</summary>
    public string? PaymentInstructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>A readable line in the founder's activity log: who did what to which client.</summary>
public class PlatformActivity : Entity
{
    public DateTime At { get; set; }
    public Guid? ActorUserId { get; set; }
    /// <summary>Email of the person, or "system" for the daily billing run and public registrations.</summary>
    public string Actor { get; set; } = "system";
    public string Action { get; set; } = default!;
    public Guid? OrgId { get; set; }
    public string? ClientName { get; set; }
    public string? Detail { get; set; }
}
