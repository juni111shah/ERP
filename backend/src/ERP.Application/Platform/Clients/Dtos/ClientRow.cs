using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record ClientRow(
    Guid OrgId, string Name, string Code, string IndustryCode, string CountryCode, DateTime CreatedAt, Guid? PlanId, string? PlanName, SubscriptionStatus? Status, SubscriptionAccessLevel Access,
    BillingCycle? Cycle, decimal? CyclePrice, decimal MonthlyValue, bool IsComplimentary, DateOnly? TrialEndsOn, DateOnly? CurrentPeriodEnd, int ActiveUsers, int? MaxUsers, DateTime? LastSignInAt, decimal OpenBalance, bool HasPlanRequest);

public sealed record ClientDetail(
    ClientRow Client, string BaseCurrency, string TimeZone, string? BillingEmail, string? ContactPhone, string? Notes, decimal? CustomPrice, DateOnly? StartedOn, DateOnly? CurrentPeriodStart, DateOnly? LapsedOn, string? SuspendedReason, bool SuspendedByFounder,
    DateTime? CancelledAt, string? CancelReason, RequestedChange? Requested, int LegalEntities, int? MaxLegalEntities, IReadOnlyList<string> EnabledModules, IReadOnlyList<string>? PlanModules, IReadOnlyList<WorkspaceMemberView> Members, IReadOnlyList<InvoiceRow> Invoices, IReadOnlyList<ActivityRow> Activity);
