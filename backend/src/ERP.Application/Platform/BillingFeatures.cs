using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Platform;

public sealed record InvoiceRow(
    Guid Id, string Number, SubscriptionInvoiceStatus Status, string Description, decimal Total, string Currency, DateOnly IssuedOn, DateOnly DueOn, DateOnly? PaidOn, bool IsOverdue);

/// <summary>An invoice with everything needed to print it.</summary>
public sealed record InvoiceDocument(
    Guid Id, string Number, SubscriptionInvoiceStatus Status, Guid OrgId, string ClientName, string ClientCode, string? BillingEmail, string Description, DateOnly? PeriodStart, DateOnly? PeriodEnd,
    decimal Subtotal, decimal TaxPercent, string TaxLabel, decimal TaxAmount, decimal Total, string Currency, DateOnly IssuedOn, DateOnly DueOn, DateOnly? PaidOn,
    string? PaymentMethod, string? PaymentReference, string? VoidReason, string CompanyName, string? CompanyAddress, string? CompanyTaxNumber, string? SupportEmail, string? PaymentInstructions);

public sealed record RequestedChange(Guid PlanId, string PlanName, BillingCycle Cycle, string? Note, DateTime RequestedAt);

public sealed record BillingOverview(
    SubscriptionSummary Summary, Guid? PlanId, BillingCycle? Cycle, decimal? CyclePrice, string Currency, int ActiveUsers, int? MaxUsers, int LegalEntities, int? MaxLegalEntities,
    RequestedChange? Requested, IReadOnlyList<PublicPlan> Plans, IReadOnlyList<InvoiceRow> Invoices, string? PaymentInstructions, string? SupportEmail, string? TermsUrl);

public sealed record PlanChangeInput(Guid PlanId, BillingCycle Cycle, string? Note);

public sealed record GetBilling : IQuery<BillingOverview>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillingRead;
}
public sealed record GetMyInvoice(Guid Id) : IQuery<InvoiceDocument>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillingRead;
}
public sealed record RequestPlanChange(PlanChangeInput Input) : ICommand<BillingOverview>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillingManage;
}
public sealed record WithdrawPlanChange : ICommand<BillingOverview>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillingManage;
}

public sealed class RequestPlanChangeValidator : AbstractValidator<RequestPlanChange>
{
    public RequestPlanChangeValidator()
    {
        RuleFor(x => x.Input.PlanId).NotEmpty().WithMessage("Choose a plan.");
        RuleFor(x => x.Input.Note).MaximumLength(400);
    }
}

/// <summary>A client's own view of its plan and invoices. Every read is filtered on the caller's workspace: these tables are not org-scoped.</summary>
internal sealed class BillingHandlers(
    ICurrentUser user, IRepository<WorkspaceSubscription> subscriptions, IReadRepository<SubscriptionPlan> plans, IReadRepository<SubscriptionInvoice> invoices,
    IReadRepository<Organization> organizations, IReadRepository<OrganizationMember> members, IReadRepository<LegalEntity> legalEntities, ISubscriptionAccess access,
    PlatformSettingsStore settingsStore, PlatformLog log, IPlatformMailer mailer, IClock clock) :
    IRequestHandler<GetBilling, BillingOverview>,
    IRequestHandler<GetMyInvoice, InvoiceDocument>,
    IRequestHandler<RequestPlanChange, BillingOverview>,
    IRequestHandler<WithdrawPlanChange, BillingOverview>
{
    private Guid OrgId => user.OrgId ?? throw new ForbiddenException();

    public async Task<BillingOverview> Handle(GetBilling q, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var orgId = OrgId;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var s = await subscriptions.Query().FirstOrDefaultAsync(x => x.OrgId == orgId, ct);
        var plan = s is null ? null : await plans.FindAsync(s.PlanId, ct);
        var available = await plans.Query().Where(p => p.IsActive && p.IsPublic).OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
        var rows = await invoices.Query().Where(i => i.OrgId == orgId).OrderByDescending(i => i.IssuedOn).ThenByDescending(i => i.Number).ToListAsync(ct);
        RequestedChange? requested = null;
        if (s?.RequestedPlanId is { } requestedId && await plans.FindAsync(requestedId, ct) is { } target)
            requested = new(target.Id, target.Name, s.RequestedCycle ?? s.Cycle, s.RequestNote, s.RequestedAt ?? clock.UtcNow);
        return new(
            await access.GetAsync(orgId, ct), s?.PlanId, s?.Cycle, s is null || plan is null ? null : SubscriptionRules.CyclePrice(s, plan), settings.Currency,
            await members.CountAsync(m => m.IsActive, ct), plan?.MaxUsers, await legalEntities.CountAsync(null, ct), plan?.MaxLegalEntities, requested,
            available.Select(p => new PublicPlan(p.Id, p.Code, p.Name, p.Description, p.MonthlyPrice, p.AnnualPrice, settings.Currency, p.TrialDays, p.MaxUsers, p.MaxLegalEntities, p.ModuleList(), p.FeatureList(), p.IsRecommended)).ToList(),
            rows.Select(i => ToRow(i, today)).ToList(), settings.PaymentInstructions, settings.SupportEmail, settings.TermsUrl);
    }

    internal static InvoiceRow ToRow(SubscriptionInvoice i, DateOnly today)
        => new(i.Id, i.Number, i.Status, i.Description, i.Total, i.Currency, i.IssuedOn, i.DueOn, i.PaidOn, i.Status == SubscriptionInvoiceStatus.Open && i.DueOn < today);

    public async Task<InvoiceDocument> Handle(GetMyInvoice q, CancellationToken ct)
    {
        var orgId = OrgId;
        var invoice = await invoices.Query().FirstOrDefaultAsync(i => i.Id == q.Id && i.OrgId == orgId, ct) ?? throw new NotFoundException();
        return ToDocument(invoice, await organizations.GetAsync(orgId, ct), await subscriptions.Query().FirstOrDefaultAsync(s => s.OrgId == orgId, ct), await settingsStore.GetAsync(ct));
    }

    internal static InvoiceDocument ToDocument(SubscriptionInvoice i, Organization client, WorkspaceSubscription? s, PlatformSetting settings) => new(
        i.Id, i.Number, i.Status, i.OrgId, client.Name, client.Code, s?.BillingEmail, i.Description, i.PeriodStart, i.PeriodEnd, i.Subtotal, i.TaxPercent, string.IsNullOrEmpty(i.TaxLabel) ? settings.TaxLabel : i.TaxLabel, i.TaxAmount, i.Total, i.Currency,
        i.IssuedOn, i.DueOn, i.PaidOn, i.PaymentMethod, i.PaymentReference, i.VoidReason, SubscriptionInvoicing.CompanyOf(settings), settings.CompanyAddress, settings.CompanyTaxNumber, settings.SupportEmail, settings.PaymentInstructions);

    public async Task<BillingOverview> Handle(RequestPlanChange cmd, CancellationToken ct)
    {
        var orgId = OrgId;
        var s = await subscriptions.Tracked().FirstOrDefaultAsync(x => x.OrgId == orgId, ct) ?? throw new ConflictException("This workspace has no subscription to change.");
        var plan = await plans.Query().FirstOrDefaultAsync(p => p.Id == cmd.Input.PlanId && p.IsActive && p.IsPublic, ct) ?? throw new FieldValidationException("planId", "Choose a plan from the list.");
        s.RequestedPlanId = plan.Id;
        s.RequestedCycle = cmd.Input.Cycle;
        s.RequestNote = cmd.Input.Note?.Trim();
        s.RequestedAt = clock.UtcNow;
        var client = await organizations.GetAsync(orgId, ct);
        log.Add("Plan change requested", orgId, client.Name, $"{plan.Name}, {(cmd.Input.Cycle == BillingCycle.Annual ? "annual" : "monthly")}. {s.RequestNote}");
        await subscriptions.SaveChangesAsync(ct);
        var settings = await settingsStore.GetAsync(ct);
        await mailer.SendAsync(settings.SupportEmail, $"Plan change request: {client.Name}", $"{client.Name} asked for the {plan.Name} plan.\n\n{s.RequestNote}\n\n{mailer.Link($"/platform/clients/{orgId}")}", ct);
        return await Handle(new GetBilling(), ct);
    }

    public async Task<BillingOverview> Handle(WithdrawPlanChange cmd, CancellationToken ct)
    {
        var orgId = OrgId;
        var s = await subscriptions.Tracked().FirstOrDefaultAsync(x => x.OrgId == orgId, ct) ?? throw new ConflictException("This workspace has no subscription.");
        s.RequestedPlanId = null; s.RequestedCycle = null; s.RequestNote = null; s.RequestedAt = null;
        await subscriptions.SaveChangesAsync(ct);
        return await Handle(new GetBilling(), ct);
    }
}
