using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record RequestedChange(Guid PlanId, string PlanName, BillingCycle Cycle, string? Note, DateTime RequestedAt);

public sealed record BillingOverview(
    SubscriptionSummary Summary, Guid? PlanId, BillingCycle? Cycle, decimal? CyclePrice, string Currency, int ActiveUsers, int? MaxUsers, int LegalEntities, int? MaxLegalEntities,
    RequestedChange? Requested, IReadOnlyList<PublicPlan> Plans, IReadOnlyList<InvoiceRow> Invoices, string? PaymentInstructions, string? SupportEmail, string? TermsUrl);

public sealed record PlanChangeInput(Guid PlanId, BillingCycle Cycle, string? Note);
