using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Platform;

// The founder's console. Every endpoint of this file sits behind the "platform-admin" policy.

public sealed record PlanView(
    Guid Id, string Code, string Name, string? Description, bool IsPublic, bool IsActive, bool IsRecommended, int SortOrder, decimal MonthlyPrice, decimal AnnualPrice,
    string Currency, int TrialDays, int? MaxUsers, int? MaxLegalEntities, IReadOnlyList<string> Modules, IReadOnlyList<string> Features, int ClientCount, int? AssistantMonthlyMessages = null);

public sealed record PlanInput(
    string Code, string Name, string? Description, bool IsPublic, bool IsRecommended, int SortOrder, decimal MonthlyPrice, decimal AnnualPrice, int TrialDays,
    int? MaxUsers, int? MaxLegalEntities, IReadOnlyList<string>? Modules, IReadOnlyList<string>? Features, int? AssistantMonthlyMessages = null);

public sealed record ClientRow(
    Guid OrgId, string Name, string Code, string IndustryCode, string CountryCode, DateTime CreatedAt, Guid? PlanId, string? PlanName, SubscriptionStatus? Status, SubscriptionAccessLevel Access,
    BillingCycle? Cycle, decimal? CyclePrice, decimal MonthlyValue, bool IsComplimentary, DateOnly? TrialEndsOn, DateOnly? CurrentPeriodEnd, int ActiveUsers, int? MaxUsers, DateTime? LastSignInAt, decimal OpenBalance, bool HasPlanRequest);

public sealed record ClientDetail(
    ClientRow Client, string BaseCurrency, string TimeZone, string? BillingEmail, string? ContactPhone, string? Notes, decimal? CustomPrice, DateOnly? StartedOn, DateOnly? CurrentPeriodStart, DateOnly? LapsedOn, string? SuspendedReason, bool SuspendedByFounder,
    DateTime? CancelledAt, string? CancelReason, RequestedChange? Requested, int LegalEntities, int? MaxLegalEntities, IReadOnlyList<string> EnabledModules, IReadOnlyList<string>? PlanModules, IReadOnlyList<WorkspaceMemberView> Members, IReadOnlyList<InvoiceRow> Invoices, IReadOnlyList<ActivityRow> Activity);

public sealed record ActivityRow(Guid Id, DateTime At, string Actor, string Action, Guid? OrgId, string? ClientName, string? Detail);

public sealed record PlanCount(Guid PlanId, string PlanName, int Clients, decimal MonthlyValue);
public sealed record MonthAmount(string Month, decimal Amount);
public sealed record ExpiringTrial(Guid OrgId, string Name, DateOnly TrialEndsOn, int DaysLeft);
public sealed record PlatformDashboard(
    string Currency, decimal MonthlyRecurring, decimal AnnualRecurring, int TotalClients, int Trialing, int Active, int PastDue, int Expired, int Suspended, int Cancelled, int WithoutPlan,
    decimal Outstanding, decimal Overdue, decimal CollectedThisMonth, decimal CollectedTotal, int PendingSignups, int NewClients30Days, int PlanRequests, int NewLeads,
    IReadOnlyList<PlanCount> ByPlan, IReadOnlyList<MonthAmount> Collected, IReadOnlyList<ExpiringTrial> TrialsEnding, IReadOnlyList<ActivityRow> RecentActivity);

public sealed record CreateClientInput(
    string WorkspaceName, string SignInKey, string Industry, string CountryCode, string BaseCurrency, string? TimeZone, string AdminName, string AdminEmail, string? Phone,
    Guid PlanId, BillingCycle Cycle, ClientStartMode StartMode, int? TrialDays, decimal? CustomPrice, string? Notes);
public sealed record CreatedClient(Guid OrgId, string SignInKey, string AdminEmail, string? TemporaryPassword);

public sealed record SetClientPlanInput(Guid PlanId, BillingCycle Cycle, decimal? CustomPrice, bool IsComplimentary, ClientStartMode? StartMode, int? TrialDays);
public sealed record ReasonInput(string? Reason);
public sealed record ExtendTrialInput(int Days);
public sealed record ActivateInput(bool IssueInvoice);
public sealed record ClientNotesInput(string? BillingEmail, string? ContactPhone, string? Notes);
public sealed record IssueInvoiceInput(string? Description, decimal? Amount, DateOnly? DueOn);
public sealed record RecordPaymentInput(DateOnly? PaidOn, string? Method, string? Reference);
public sealed record TemporaryPassword(string Password);

public sealed record SignupRow(
    Guid Id, SignupStatus Status, string BusinessName, string SignInKey, string Industry, string CountryCode, string BaseCurrency, string ContactName, string Email, string? Phone,
    Guid PlanId, string PlanName, BillingCycle Cycle, DateTime? EmailVerifiedAt, DateTime CreatedAt, DateTime? ReviewedAt, string? RejectReason, Guid? ProvisionedOrgId);

public sealed record PlatformSettingsView(
    SignupMode SignupMode, bool RequireEmailVerification, bool EmailServerConfigured, Guid? DefaultPlanId, string Currency, decimal TaxPercent, string TaxLabel, string InvoicePrefix, int NextInvoiceNumber, int InvoiceDueDays, int RenewalLeadDays, int GraceDays, int SuspendAfterDays,
    string CompanyName, string? CompanyAddress, string? CompanyTaxNumber, string? SupportEmail, string? TermsUrl, string? PaymentInstructions, string TermsText, string PrivacyText, bool LegalIsStarterText, string? AboutText);
public sealed record PlatformSettingsInput(
    SignupMode SignupMode, bool RequireEmailVerification, Guid? DefaultPlanId, string Currency, decimal TaxPercent, string TaxLabel, string InvoicePrefix, int InvoiceDueDays, int RenewalLeadDays, int GraceDays, int SuspendAfterDays,
    string? CompanyName, string? CompanyAddress, string? CompanyTaxNumber, string? SupportEmail, string? TermsUrl, string? PaymentInstructions, string? TermsText = null, string? PrivacyText = null, string? AboutText = null);
public sealed record TeamMemberInput(string Email, string FullName);

public sealed record GetPlatformDashboard : IQuery<PlatformDashboard>;
public sealed record ListPlans(bool IncludeArchived = true) : IQuery<IReadOnlyList<PlanView>>;
public sealed record CreatePlan(PlanInput Input) : ICommand<PlanView>;
public sealed record UpdatePlan(Guid Id, PlanInput Input) : ICommand<PlanView>;
public sealed record SetPlanArchived(Guid Id, bool Archived) : ICommand<PlanView>;

public sealed record ListClients : PagedQuery, IQuery<PagedResult<ClientRow>>
{
    public string? Status { get; init; }
    public Guid? PlanId { get; init; }
}
public sealed record GetClient(Guid OrgId) : IQuery<ClientDetail>;
public sealed record CreateClient(CreateClientInput Input) : ICommand<CreatedClient>;
public sealed record SetClientPlan(Guid OrgId, SetClientPlanInput Input) : ICommand<ClientDetail>;
public sealed record ExtendClientTrial(Guid OrgId, int Days) : ICommand<ClientDetail>;
public sealed record ActivateClient(Guid OrgId, bool IssueInvoice) : ICommand<ClientDetail>;
public sealed record SuspendClient(Guid OrgId, string? Reason) : ICommand<ClientDetail>;
public sealed record ReactivateClient(Guid OrgId) : ICommand<ClientDetail>;
public sealed record CancelClient(Guid OrgId, string? Reason) : ICommand<ClientDetail>;
public sealed record DeclinePlanRequest(Guid OrgId) : ICommand<ClientDetail>;
public sealed record UpdateClientNotes(Guid OrgId, ClientNotesInput Input) : ICommand<ClientDetail>;
public sealed record IssueClientInvoice(Guid OrgId, IssueInvoiceInput Input) : ICommand<InvoiceRow>;
public sealed record ResetClientUserPassword(Guid OrgId, Guid UserId) : ICommand<TemporaryPassword>;

public sealed record ListPlatformInvoices : PagedQuery, IQuery<PagedResult<PlatformInvoiceRow>>
{
    public string? Status { get; init; }
    public Guid? OrgId { get; init; }
}
public sealed record PlatformInvoiceRow(
    Guid Id, string Number, SubscriptionInvoiceStatus Status, Guid OrgId, string ClientName, string Description, decimal Total, string Currency, DateOnly IssuedOn, DateOnly DueOn, DateOnly? PaidOn, string? PaymentMethod, string? PaymentReference, bool IsOverdue);
public sealed record GetPlatformInvoice(Guid Id) : IQuery<InvoiceDocument>;
public sealed record RecordInvoicePayment(Guid Id, RecordPaymentInput Input) : ICommand<InvoiceDocument>;
public sealed record VoidPlatformInvoice(Guid Id, string? Reason) : ICommand<InvoiceDocument>;
public sealed record ReopenPlatformInvoice(Guid Id) : ICommand<InvoiceDocument>;

public sealed record ListSignups : PagedQuery, IQuery<PagedResult<SignupRow>>
{
    public string? Status { get; init; }
}
public sealed record ApproveSignup(Guid Id) : ICommand<SignupRow>;
public sealed record RejectSignup(Guid Id, string? Reason) : ICommand<SignupRow>;

public sealed record GetPlatformSettings : IQuery<PlatformSettingsView>;
public sealed record UpdatePlatformSettings(PlatformSettingsInput Input) : ICommand<PlatformSettingsView>;
public sealed record ListPlatformActivity : PagedQuery, IQuery<PagedResult<ActivityRow>>
{
    public Guid? OrgId { get; init; }
}
public sealed record RunBillingNow : ICommand<LifecycleResult>;
public sealed record ListPlatformTeam : IQuery<IReadOnlyList<PlatformTeamMember>>;
public sealed record AddPlatformTeamMember(TeamMemberInput Input) : ICommand<NewTeamMember>;
public sealed record RemovePlatformTeamMember(Guid UserId) : ICommand;

public sealed class PlanInputValidator : AbstractValidator<PlanInput>
{
    public PlanInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Enter a plan code.").Matches("^[a-z0-9][a-z0-9-]{0,38}$").WithMessage("Use lower-case letters, digits or dashes.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter a plan name.").MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0).WithMessage("The price cannot be negative.");
        RuleFor(x => x.AnnualPrice).GreaterThanOrEqualTo(0).WithMessage("The price cannot be negative.");
        RuleFor(x => x.TrialDays).InclusiveBetween(0, 365).WithMessage("Use 0 to 365 days.");
        RuleFor(x => x.MaxUsers).GreaterThan(0).When(x => x.MaxUsers is not null).WithMessage("Leave empty for no limit, or use at least 1.");
        RuleFor(x => x.MaxLegalEntities).GreaterThan(0).When(x => x.MaxLegalEntities is not null).WithMessage("Leave empty for no limit, or use at least 1.");
        RuleFor(x => x.AssistantMonthlyMessages).GreaterThanOrEqualTo(0).When(x => x.AssistantMonthlyMessages is not null).WithMessage("Leave empty for no limit, or use 0 to leave the assistant out of this plan.");
    }
}
public sealed class CreatePlanValidator : AbstractValidator<CreatePlan> { public CreatePlanValidator() => RuleFor(x => x.Input).SetValidator(new PlanInputValidator()); }
public sealed class UpdatePlanValidator : AbstractValidator<UpdatePlan> { public UpdatePlanValidator() => RuleFor(x => x.Input).SetValidator(new PlanInputValidator()); }

public sealed class CreateClientValidator : AbstractValidator<CreateClient>
{
    public CreateClientValidator()
    {
        RuleFor(x => x.Input.WorkspaceName).NotEmpty().WithMessage("Enter the business name.").MaximumLength(200);
        RuleFor(x => x.Input.SignInKey).NotEmpty().WithMessage("Enter a workspace code.");
        RuleFor(x => x.Input.Industry).NotEmpty().WithMessage("Choose an industry.");
        RuleFor(x => x.Input.CountryCode).Length(2).WithMessage("Choose a country.");
        RuleFor(x => x.Input.BaseCurrency).Length(3).WithMessage("Choose a currency.");
        RuleFor(x => x.Input.AdminName).NotEmpty().WithMessage("Enter the administrator's name.").MaximumLength(200);
        RuleFor(x => x.Input.AdminEmail).NotEmpty().WithMessage("Enter the administrator's email.").EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(x => x.Input.PlanId).NotEmpty().WithMessage("Choose a plan.");
        RuleFor(x => x.Input.TrialDays).InclusiveBetween(1, 365).When(x => x.Input.TrialDays is not null).WithMessage("Use 1 to 365 days.");
        RuleFor(x => x.Input.CustomPrice).GreaterThanOrEqualTo(0).When(x => x.Input.CustomPrice is not null).WithMessage("The price cannot be negative.");
    }
}

public sealed class UpdatePlatformSettingsValidator : AbstractValidator<UpdatePlatformSettings>
{
    public UpdatePlatformSettingsValidator()
    {
        RuleFor(x => x.Input.Currency).Length(3).WithMessage("Use a 3-letter currency code.");
        RuleFor(x => x.Input.TaxPercent).InclusiveBetween(0, 100).WithMessage("Use 0 to 100.");
        RuleFor(x => x.Input.TaxLabel).NotEmpty().WithMessage("Enter a tax label, such as VAT.").MaximumLength(40);
        RuleFor(x => x.Input.InvoicePrefix).NotEmpty().WithMessage("Enter an invoice prefix.").Matches("^[A-Za-z0-9]{1,10}$").WithMessage("Use up to 10 letters or digits.");
        RuleFor(x => x.Input.InvoiceDueDays).InclusiveBetween(0, 120).WithMessage("Use 0 to 120 days.");
        RuleFor(x => x.Input.RenewalLeadDays).InclusiveBetween(0, 60).WithMessage("Use 0 to 60 days.");
        RuleFor(x => x.Input.GraceDays).InclusiveBetween(0, 120).WithMessage("Use 0 to 120 days.");
        RuleFor(x => x.Input.SuspendAfterDays).InclusiveBetween(1, 365).WithMessage("Use 1 to 365 days.");
        RuleFor(x => x.Input).Must(i => i.SuspendAfterDays > i.GraceDays).WithName("suspendAfterDays").WithMessage("Suspension must come after the grace period.");
        RuleFor(x => x.Input.PaymentInstructions).MaximumLength(2000);
        RuleFor(x => x.Input.AboutText).MaximumLength(2000);
        RuleFor(x => x.Input.TermsText).MaximumLength(100_000);
        RuleFor(x => x.Input.PrivacyText).MaximumLength(100_000);
        RuleFor(x => x.Input.TermsUrl).Must(u => Uri.TryCreate(u, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https").When(x => !string.IsNullOrWhiteSpace(x.Input.TermsUrl)).WithMessage("Enter a full web address, starting with https://.");
        RuleFor(x => x.Input.SupportEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Input.SupportEmail)).WithMessage("Enter a valid email address.");
    }
}

public sealed class IssueClientInvoiceValidator : AbstractValidator<IssueClientInvoice>
{
    public IssueClientInvoiceValidator()
    {
        RuleFor(x => x.Input.Amount).GreaterThan(0).When(x => x.Input.Amount is not null).WithMessage("Enter an amount above zero.");
        RuleFor(x => x.Input.Description).NotEmpty().When(x => x.Input.Amount is not null).WithMessage("Describe what this charge is for.");
    }
}

public sealed class TeamMemberValidator : AbstractValidator<AddPlatformTeamMember>
{
    public TeamMemberValidator()
    {
        RuleFor(x => x.Input.Email).NotEmpty().WithMessage("Enter an email.").EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(x => x.Input.FullName).NotEmpty().WithMessage("Enter a name.").MaximumLength(200);
    }
}

internal sealed class PlanHandlers(IRepository<SubscriptionPlan> plans, IReadRepository<WorkspaceSubscription> subscriptions, PlatformSettingsStore settingsStore, PlatformLog log) :
    IRequestHandler<ListPlans, IReadOnlyList<PlanView>>,
    IRequestHandler<CreatePlan, PlanView>,
    IRequestHandler<UpdatePlan, PlanView>,
    IRequestHandler<SetPlanArchived, PlanView>
{
    public async Task<IReadOnlyList<PlanView>> Handle(ListPlans q, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var counts = await subscriptions.Query().Where(s => s.Status != SubscriptionStatus.Cancelled).GroupBy(s => s.PlanId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var rows = await plans.Query().OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
        return rows.Where(p => q.IncludeArchived || p.IsActive).Select(p => ToView(p, settings.Currency, counts.GetValueOrDefault(p.Id))).ToList();
    }

    private async Task<PlanView> ViewAsync(SubscriptionPlan p, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        return ToView(p, settings.Currency, await subscriptions.CountAsync(s => s.PlanId == p.Id && s.Status != SubscriptionStatus.Cancelled, ct));
    }

    private static PlanView ToView(SubscriptionPlan p, string currency, int clients) => new(
        p.Id, p.Code, p.Name, p.Description, p.IsPublic, p.IsActive, p.IsRecommended, p.SortOrder, p.MonthlyPrice, p.AnnualPrice, currency, p.TrialDays, p.MaxUsers, p.MaxLegalEntities, p.ModuleList(), p.FeatureList(), clients, p.AssistantMonthlyMessages);

    private static void Apply(SubscriptionPlan p, PlanInput i)
    {
        p.Code = i.Code; p.Name = i.Name.Trim(); p.Description = i.Description?.Trim(); p.IsPublic = i.IsPublic; p.IsRecommended = i.IsRecommended; p.SortOrder = i.SortOrder;
        p.MonthlyPrice = i.MonthlyPrice; p.AnnualPrice = i.AnnualPrice; p.TrialDays = i.TrialDays; p.MaxUsers = i.MaxUsers; p.MaxLegalEntities = i.MaxLegalEntities; p.AssistantMonthlyMessages = i.AssistantMonthlyMessages;
        p.ModuleKeys = i.Modules is { Count: > 0 } m ? string.Join(",", m.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct()) : null;
        p.Features = i.Features is { Count: > 0 } f ? string.Join("\n", f.Select(x => x.Trim()).Where(x => x.Length > 0)) : null;
    }

    public async Task<PlanView> Handle(CreatePlan cmd, CancellationToken ct)
    {
        if (await plans.AnyAsync(p => p.Code == cmd.Input.Code, ct)) throw new FieldValidationException("code", "That plan code is already used.");
        var plan = new SubscriptionPlan();
        Apply(plan, cmd.Input);
        plans.Add(plan);
        log.Add("Plan created", null, null, $"{plan.Name}: {plan.MonthlyPrice:N2} a month, {plan.AnnualPrice:N2} a year");
        await plans.SaveChangesAsync(ct);
        return await ViewAsync(plan, ct);
    }

    public async Task<PlanView> Handle(UpdatePlan cmd, CancellationToken ct)
    {
        var plan = await plans.GetForUpdateAsync(cmd.Id, ct);
        if (await plans.AnyAsync(p => p.Code == cmd.Input.Code && p.Id != cmd.Id, ct)) throw new FieldValidationException("code", "That plan code is already used.");
        var was = (plan.MonthlyPrice, plan.AnnualPrice);
        Apply(plan, cmd.Input);
        log.Add("Plan updated", null, null, was == (plan.MonthlyPrice, plan.AnnualPrice) ? plan.Name : $"{plan.Name}: price {was.MonthlyPrice:N2}/{was.AnnualPrice:N2} changed to {plan.MonthlyPrice:N2}/{plan.AnnualPrice:N2}");
        await plans.SaveChangesAsync(ct);
        return await ViewAsync(plan, ct);
    }

    public async Task<PlanView> Handle(SetPlanArchived cmd, CancellationToken ct)
    {
        var plan = await plans.GetForUpdateAsync(cmd.Id, ct);
        if (cmd.Archived && (await settingsStore.GetAsync(ct)).DefaultPlanId == plan.Id) throw new ConflictException("This is the default plan for new registrations. Choose another default first.");
        plan.IsActive = !cmd.Archived;
        log.Add(cmd.Archived ? "Plan archived" : "Plan restored", null, null, plan.Name);
        await plans.SaveChangesAsync(ct);
        return await ViewAsync(plan, ct);
    }
}

internal sealed class ClientHandlers(
    IRepository<WorkspaceSubscription> subscriptions, IReadRepository<SubscriptionPlan> plans, IRepository<SubscriptionInvoice> invoices, IReadRepository<Organization> organizations, IReadRepository<PlatformActivity> activity,
    IRepository<SignupRequest> signups, IReadRepository<BrandLead> leads, IOnboardingService onboarding, IPlatformDirectory directory, ISubscriptionAccess access, ClientProvisioner provisioner, SubscriptionInvoicing invoicing, PlatformSettingsStore settingsStore,
    PlatformLog log, IPlatformMailer mailer, IClock clock, ICurrentUser user, ERP.Application.Auth.IAuthService auth) :
    IRequestHandler<GetPlatformDashboard, PlatformDashboard>,
    IRequestHandler<ListClients, PagedResult<ClientRow>>,
    IRequestHandler<GetClient, ClientDetail>,
    IRequestHandler<CreateClient, CreatedClient>,
    IRequestHandler<SetClientPlan, ClientDetail>,
    IRequestHandler<ExtendClientTrial, ClientDetail>,
    IRequestHandler<ActivateClient, ClientDetail>,
    IRequestHandler<SuspendClient, ClientDetail>,
    IRequestHandler<ReactivateClient, ClientDetail>,
    IRequestHandler<CancelClient, ClientDetail>,
    IRequestHandler<DeclinePlanRequest, ClientDetail>,
    IRequestHandler<UpdateClientNotes, ClientDetail>,
    IRequestHandler<IssueClientInvoice, InvoiceRow>,
    IRequestHandler<ResetClientUserPassword, TemporaryPassword>,
    IRequestHandler<ListPlatformInvoices, PagedResult<PlatformInvoiceRow>>,
    IRequestHandler<GetPlatformInvoice, InvoiceDocument>,
    IRequestHandler<RecordInvoicePayment, InvoiceDocument>,
    IRequestHandler<VoidPlatformInvoice, InvoiceDocument>,
    IRequestHandler<ReopenPlatformInvoice, InvoiceDocument>,
    IRequestHandler<ListSignups, PagedResult<SignupRow>>,
    IRequestHandler<ApproveSignup, SignupRow>,
    IRequestHandler<RejectSignup, SignupRow>,
    IRequestHandler<ListPlatformActivity, PagedResult<ActivityRow>>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    private static ActivityRow ToRow(PlatformActivity a) => new(a.Id, a.At, a.Actor, a.Action, a.OrgId, a.ClientName, a.Detail);

    private async Task<List<ClientRow>> RowsAsync(Guid? onlyOrg, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var orgs = await organizations.Query().Where(o => !o.IsInternal && (onlyOrg == null || o.Id == onlyOrg)).ToListAsync(ct);
        var subs = await subscriptions.Query().ToDictionaryAsync(s => s.OrgId, ct);
        var planById = await plans.Query().ToDictionaryAsync(p => p.Id, ct);
        var usage = await directory.UsageAsync(onlyOrg is { } id ? [id] : null, ct);
        var balances = (await invoices.Query().Where(i => i.Status == SubscriptionInvoiceStatus.Open).Select(i => new { i.OrgId, i.Total }).ToListAsync(ct))
            .GroupBy(i => i.OrgId).ToDictionary(g => g.Key, g => g.Sum(x => x.Total));
        return orgs.Select(o =>
        {
            var s = subs.GetValueOrDefault(o.Id);
            var plan = s is null ? null : planById.GetValueOrDefault(s.PlanId);
            var u = usage.GetValueOrDefault(o.Id);
            return new ClientRow(
                o.Id, o.Name, o.Code, o.IndustryCode, o.CountryCode, o.CreatedAt, s?.PlanId, plan?.Name, s?.Status, s is null ? SubscriptionAccessLevel.Full : SubscriptionRules.AccessOf(s, settings, Today),
                s?.Cycle, s is null || plan is null ? null : SubscriptionRules.CyclePrice(s, plan), s is null || plan is null ? 0 : SubscriptionRules.MonthlyValue(s, plan), s?.IsComplimentary ?? false,
                s?.TrialEndsOn, s?.CurrentPeriodEnd, u?.ActiveUsers ?? 0, plan?.MaxUsers, u?.LastSignInAt, balances.GetValueOrDefault(o.Id), s?.RequestedPlanId is not null);
        }).ToList();
    }

    public async Task<PlatformDashboard> Handle(GetPlatformDashboard q, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var rows = await RowsAsync(null, ct);
        var today = Today;
        var allInvoices = await invoices.Query().Where(i => i.Status != SubscriptionInvoiceStatus.Void).Select(i => new { i.Status, i.Total, i.DueOn, i.PaidOn }).ToListAsync(ct);
        var paid = allInvoices.Where(i => i.Status == SubscriptionInvoiceStatus.Paid && i.PaidOn is not null).ToList();
        var open = allInvoices.Where(i => i.Status == SubscriptionInvoiceStatus.Open).ToList();
        bool Paying(ClientRow r) => r.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue && !r.IsComplimentary;
        var mrr = rows.Where(Paying).Sum(r => r.MonthlyValue);
        var months = Enumerable.Range(0, 6).Select(n => new DateOnly(today.Year, today.Month, 1).AddMonths(-5 + n)).ToList();
        var since = clock.UtcNow.AddDays(-30);
        var planNames = await plans.Query().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return new(
            settings.Currency, mrr, mrr * 12, rows.Count, rows.Count(r => r.Status == SubscriptionStatus.Trialing), rows.Count(r => r.Status == SubscriptionStatus.Active), rows.Count(r => r.Status == SubscriptionStatus.PastDue),
            rows.Count(r => r.Status == SubscriptionStatus.Expired), rows.Count(r => r.Status == SubscriptionStatus.Suspended), rows.Count(r => r.Status == SubscriptionStatus.Cancelled), rows.Count(r => r.Status is null),
            open.Sum(i => i.Total), open.Where(i => i.DueOn < today).Sum(i => i.Total), paid.Where(i => i.PaidOn!.Value.Year == today.Year && i.PaidOn!.Value.Month == today.Month).Sum(i => i.Total), paid.Sum(i => i.Total),
            await signups.CountAsync(r => r.Status == SignupStatus.PendingReview, ct), rows.Count(r => r.CreatedAt >= since), rows.Count(r => r.HasPlanRequest), await leads.CountAsync(l => l.Status == BrandLeadStatus.New, ct),
            rows.Where(r => r.PlanId is not null && r.Status != SubscriptionStatus.Cancelled).GroupBy(r => r.PlanId!.Value).Select(g => new PlanCount(g.Key, planNames.GetValueOrDefault(g.Key, "Plan"), g.Count(), g.Where(Paying).Sum(r => r.MonthlyValue))).OrderByDescending(p => p.Clients).ToList(),
            months.Select(m => new MonthAmount(m.ToString("yyyy-MM"), paid.Where(i => i.PaidOn!.Value.Year == m.Year && i.PaidOn!.Value.Month == m.Month).Sum(i => i.Total))).ToList(),
            rows.Where(r => r.Status == SubscriptionStatus.Trialing && r.TrialEndsOn is { } e && e.DayNumber - today.DayNumber <= 7).OrderBy(r => r.TrialEndsOn).Take(10).Select(r => new ExpiringTrial(r.OrgId, r.Name, r.TrialEndsOn!.Value, r.TrialEndsOn!.Value.DayNumber - today.DayNumber)).ToList(),
            (await activity.Query().OrderByDescending(a => a.At).Take(10).ToListAsync(ct)).Select(ToRow).ToList());
    }

    public async Task<PagedResult<ClientRow>> Handle(ListClients q, CancellationToken ct)
    {
        IEnumerable<ClientRow> rows = await RowsAsync(null, ct);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            rows = rows.Where(r => r.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || r.Code.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        if (q.Status is { Length: > 0 } status)
        {
            if (status.Equals("none", StringComparison.OrdinalIgnoreCase)) rows = rows.Where(r => r.Status is null);
            else if (status.Equals("planRequest", StringComparison.OrdinalIgnoreCase)) rows = rows.Where(r => r.HasPlanRequest);
            else rows = rows.Where(r => string.Equals(r.Status?.ToString(), status, StringComparison.OrdinalIgnoreCase));
        }
        if (q.PlanId is { } planId) rows = rows.Where(r => r.PlanId == planId);
        rows = rows.OrderByDescending(r => r.CreatedAt);
        var list = rows.ToList();
        return new(list.Skip((q.PageNumber - 1) * q.Size).Take(q.Size).ToList(), list.Count, q.PageNumber, q.Size);
    }

    public async Task<ClientDetail> Handle(GetClient q, CancellationToken ct)
    {
        var row = (await RowsAsync(q.OrgId, ct)).FirstOrDefault() ?? throw new NotFoundException();
        var org = await organizations.GetAsync(q.OrgId, ct);
        var s = await subscriptions.Query().FirstOrDefaultAsync(x => x.OrgId == q.OrgId, ct);
        var plan = s is null ? null : await plans.FindAsync(s.PlanId, ct);
        var usage = (await directory.UsageAsync([q.OrgId], ct)).GetValueOrDefault(q.OrgId);
        RequestedChange? requested = null;
        if (s?.RequestedPlanId is { } requestedId && await plans.FindAsync(requestedId, ct) is { } target)
            requested = new(target.Id, target.Name, s.RequestedCycle ?? s.Cycle, s.RequestNote, s.RequestedAt ?? clock.UtcNow);
        var rows = await invoices.Query().Where(i => i.OrgId == q.OrgId).OrderByDescending(i => i.IssuedOn).ThenByDescending(i => i.Number).ToListAsync(ct);
        var log = await activity.Query().Where(a => a.OrgId == q.OrgId).OrderByDescending(a => a.At).Take(30).ToListAsync(ct);
        return new(row, org.BaseCurrency, org.TimeZone, s?.BillingEmail, s?.ContactPhone, s?.Notes, s?.CustomPrice, s?.StartedOn, s?.CurrentPeriodStart, s?.LapsedOn, s?.SuspendedReason, s?.SuspendedByFounder ?? false, s?.CancelledAt, s?.CancelReason, requested,
            usage?.LegalEntities ?? 0, plan?.MaxLegalEntities, usage?.EnabledModules ?? [], plan?.ModuleList(), await directory.MembersAsync(q.OrgId, ct), rows.Select(i => BillingHandlers.ToRow(i, Today)).ToList(), log.Select(ToRow).ToList());
    }

    public async Task<CreatedClient> Handle(CreateClient cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var plan = await plans.FindAsync(i.PlanId, ct);
        if (plan is null || !plan.IsActive) throw new FieldValidationException("planId", "Choose an active plan.");
        var subscription = SubscriptionRules.Start(plan, i.Cycle, Today, i.StartMode, i.TrialDays);
        subscription.CustomPrice = i.CustomPrice;
        subscription.BillingEmail = i.AdminEmail.Trim();
        subscription.ContactPhone = i.Phone?.Trim();
        subscription.Notes = i.Notes?.Trim();
        var password = directory.NewTemporaryPassword();
        var result = await onboarding.CreateWorkspaceAsync(
            new OnboardingRequest(i.WorkspaceName, i.SignInKey, i.Industry, i.CountryCode, i.BaseCurrency, i.TimeZone, i.WorkspaceName, i.AdminEmail, i.AdminName, password),
            new OnboardingOptions(null, TrustExistingAccount: true, AdminMustChangePassword: true, Subscription: subscription, AllowedModules: plan.ModuleList()), ct);
        log.Add("Client created", result.OrgId, i.WorkspaceName.Trim(), $"By hand. {plan.Name} plan, first administrator {i.AdminEmail.Trim()}.");
        await subscriptions.SaveChangesAsync(ct);
        await provisioner.IssueFirstInvoiceAsync(subscription, plan, Today, ct);
        return new(result.OrgId, i.SignInKey.Trim().ToLowerInvariant(), i.AdminEmail.Trim(), result.AdminAccountCreated ? password : null);
    }

    private async Task<(Organization Org, WorkspaceSubscription Sub)> LoadAsync(Guid orgId, CancellationToken ct)
    {
        var org = await organizations.GetAsync(orgId, ct);
        if (org.IsInternal) throw new ConflictException("The platform's own workspace has no subscription.");
        var sub = await subscriptions.Tracked().FirstOrDefaultAsync(s => s.OrgId == orgId, ct) ?? throw new ConflictException("This client has no plan yet. Assign one first.");
        return (org, sub);
    }

    private async Task<ClientDetail> SavedAsync(Guid orgId, CancellationToken ct)
    {
        await subscriptions.SaveChangesAsync(ct);
        access.Invalidate(orgId);
        return await Handle(new GetClient(orgId), ct);
    }

    private Task<bool> HasOverdueAsync(Guid orgId, Guid? except, CancellationToken ct)
    {
        var today = Today;
        return invoices.AnyAsync(i => i.OrgId == orgId && i.Status == SubscriptionInvoiceStatus.Open && i.DueOn < today && i.Id != except, ct);
    }

    public async Task<ClientDetail> Handle(SetClientPlan cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var org = await organizations.GetAsync(cmd.OrgId, ct);
        if (org.IsInternal) throw new ConflictException("The platform's own workspace has no subscription.");
        var plan = await plans.FindAsync(i.PlanId, ct);
        if (plan is null) throw new FieldValidationException("planId", "Choose a plan.");
        if (i.CustomPrice is < 0) throw new FieldValidationException("customPrice", "The price cannot be negative.");
        var s = await subscriptions.Tracked().FirstOrDefaultAsync(x => x.OrgId == cmd.OrgId, ct);
        var isNew = s is null;
        if (s is null)
        {
            if (!plan.IsActive) throw new FieldValidationException("planId", "Choose an active plan.");
            s = SubscriptionRules.Start(plan, i.Cycle, Today, i.IsComplimentary ? ClientStartMode.Complimentary : i.StartMode ?? ClientStartMode.Trial, i.TrialDays);
            s.OrgId = cmd.OrgId;
            subscriptions.Add(s);
        }
        else if (!plan.IsActive && plan.Id != s.PlanId) throw new FieldValidationException("planId", "That plan is archived. Choose an active plan.");

        var wasPlan = await plans.FindAsync(s.PlanId, ct);
        s.PlanId = plan.Id;
        s.Cycle = i.Cycle;
        s.CustomPrice = i.CustomPrice;
        if (i.IsComplimentary && !s.IsComplimentary) { s.Status = s.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Suspended ? s.Status : SubscriptionStatus.Active; s.LapsedOn = null; }
        s.IsComplimentary = i.IsComplimentary;
        if (s.RequestedPlanId == plan.Id) { s.RequestedPlanId = null; s.RequestedCycle = null; s.RequestNote = null; s.RequestedAt = null; }
        var modules = plan.ModuleList();
        log.Add("Plan changed", cmd.OrgId, org.Name, $"{wasPlan?.Name ?? "None"} to {plan.Name}, {(i.Cycle == BillingCycle.Annual ? "annual" : "monthly")}{(i.CustomPrice is { } price ? $", custom price {price:N2}" : "")}{(i.IsComplimentary ? ", free of charge" : "")}.");
        await subscriptions.SaveChangesAsync(ct);
        access.Invalidate(cmd.OrgId);
        if (modules.Count > 0) await directory.DisableModulesOutsideAsync(cmd.OrgId, modules, ct);
        // A first plan that starts as paid bills its first period, the same as a client created with that plan.
        if (isNew) await provisioner.IssueFirstInvoiceAsync(s, plan, Today, ct);
        return await Handle(new GetClient(cmd.OrgId), ct);
    }

    public async Task<ClientDetail> Handle(ExtendClientTrial cmd, CancellationToken ct)
    {
        if (cmd.Days is < 1 or > 365) throw new FieldValidationException("days", "Use 1 to 365 days.");
        var (org, s) = await LoadAsync(cmd.OrgId, ct);
        if (s.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Cancelled) throw new ConflictException("Only a trial, or a workspace whose trial has lapsed, can get more trial days.");
        if (s.Status == SubscriptionStatus.Suspended && s.SuspendedByFounder) throw new ConflictException("Reactivate this client first.");
        var from = s.TrialEndsOn is { } end && end > Today ? end : Today;
        s.TrialEndsOn = from.AddDays(cmd.Days);
        s.Status = SubscriptionStatus.Trialing;
        s.LapsedOn = null; s.SuspendedAt = null; s.SuspendedReason = null; s.LastReminderKey = null;
        log.Add("Trial extended", cmd.OrgId, org.Name, $"{cmd.Days} more days, to {s.TrialEndsOn:d MMM yyyy}.");
        return await SavedAsync(cmd.OrgId, ct);
    }

    public async Task<ClientDetail> Handle(ActivateClient cmd, CancellationToken ct)
    {
        var (org, s) = await LoadAsync(cmd.OrgId, ct);
        var plan = await plans.GetAsync(s.PlanId, ct);
        var today = Today;
        var running = s.Status == SubscriptionStatus.Active && s.CurrentPeriodEnd is { } end && end >= today;
        if (running) throw new ConflictException("This client is already active.");
        s.Status = SubscriptionStatus.Active;
        s.CurrentPeriodStart = today;
        s.CurrentPeriodEnd = SubscriptionRules.PeriodEnd(today, s.Cycle);
        s.LapsedOn = null; s.SuspendedAt = null; s.SuspendedReason = null; s.SuspendedByFounder = false; s.CancelledAt = null; s.CancelReason = null; s.LastReminderKey = null;
        SubscriptionInvoice? invoice = null;
        var settings = await settingsStore.GetTrackedAsync(ct);
        if (cmd.IssueInvoice && SubscriptionRules.CyclePrice(s, plan) > 0)
            invoice = invoicing.IssueForPeriod(s, plan, settings, today, today, today.AddDays(settings.InvoiceDueDays));
        log.Add("Activated", cmd.OrgId, org.Name, invoice is null ? $"Paid period to {s.CurrentPeriodEnd:d MMM yyyy}, no invoice." : $"Paid period to {s.CurrentPeriodEnd:d MMM yyyy}, invoice {invoice.Number}.");
        var detail = await SavedAsync(cmd.OrgId, ct);
        if (invoice is not null) await invoicing.NotifyAsync(invoice, s, settings, ct);
        return detail;
    }

    public async Task<ClientDetail> Handle(SuspendClient cmd, CancellationToken ct)
    {
        var (org, s) = await LoadAsync(cmd.OrgId, ct);
        if (s.Status == SubscriptionStatus.Cancelled) throw new ConflictException("This client is cancelled.");
        s.Status = SubscriptionStatus.Suspended;
        s.SuspendedAt = clock.UtcNow;
        s.SuspendedReason = cmd.Reason?.Trim();
        s.SuspendedByFounder = true;
        log.Add("Suspended", cmd.OrgId, org.Name, s.SuspendedReason);
        var detail = await SavedAsync(cmd.OrgId, ct);
        await mailer.SendAsync(s.BillingEmail, "Your workspace is suspended", $"{org.Name} has been suspended. Your data is kept.{(s.SuspendedReason is { Length: > 0 } r ? $"\n\nReason: {r}" : "")}", ct);
        return detail;
    }

    public async Task<ClientDetail> Handle(ReactivateClient cmd, CancellationToken ct)
    {
        var (org, s) = await LoadAsync(cmd.OrgId, ct);
        if (s.Status is not (SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled)) throw new ConflictException("This client is not suspended or cancelled.");
        SubscriptionRules.Reactivate(s, await HasOverdueAsync(cmd.OrgId, null, ct), Today);
        log.Add("Reactivated", cmd.OrgId, org.Name, $"Now {SubscriptionRules.StatusWords(s.Status)}.");
        var detail = await SavedAsync(cmd.OrgId, ct);
        await mailer.SendAsync(s.BillingEmail, "Your workspace is back", $"{org.Name} is available again: {mailer.Link("/auth/login")}", ct);
        return detail;
    }

    public async Task<ClientDetail> Handle(CancelClient cmd, CancellationToken ct)
    {
        var (org, s) = await LoadAsync(cmd.OrgId, ct);
        s.Status = SubscriptionStatus.Cancelled;
        s.CancelledAt = clock.UtcNow;
        s.CancelReason = cmd.Reason?.Trim();
        log.Add("Cancelled", cmd.OrgId, org.Name, s.CancelReason);
        return await SavedAsync(cmd.OrgId, ct);
    }

    public async Task<ClientDetail> Handle(DeclinePlanRequest cmd, CancellationToken ct)
    {
        var (org, s) = await LoadAsync(cmd.OrgId, ct);
        s.RequestedPlanId = null; s.RequestedCycle = null; s.RequestNote = null; s.RequestedAt = null;
        log.Add("Plan change declined", cmd.OrgId, org.Name);
        return await SavedAsync(cmd.OrgId, ct);
    }

    public async Task<ClientDetail> Handle(UpdateClientNotes cmd, CancellationToken ct)
    {
        var (org, s) = await LoadAsync(cmd.OrgId, ct);
        s.BillingEmail = cmd.Input.BillingEmail?.Trim();
        s.ContactPhone = cmd.Input.ContactPhone?.Trim();
        s.Notes = cmd.Input.Notes?.Trim();
        log.Add("Client details updated", cmd.OrgId, org.Name);
        return await SavedAsync(cmd.OrgId, ct);
    }

    public async Task<InvoiceRow> Handle(IssueClientInvoice cmd, CancellationToken ct)
    {
        var (org, s) = await LoadAsync(cmd.OrgId, ct);
        var settings = await settingsStore.GetTrackedAsync(ct);
        var today = Today;
        var due = cmd.Input.DueOn ?? today.AddDays(settings.InvoiceDueDays);
        SubscriptionInvoice invoice;
        if (cmd.Input.Amount is { } amount)
        {
            invoice = invoicing.Issue(s, settings, amount, cmd.Input.Description!.Trim(), null, null, today, due);
        }
        else
        {
            var plan = await plans.GetAsync(s.PlanId, ct);
            if (SubscriptionRules.CyclePrice(s, plan) <= 0) throw new ConflictException("This client has nothing to bill: the price is zero.");
            var start = s.CurrentPeriodEnd is { } end && end >= today ? end.AddDays(1) : today;
            if (await invoices.AnyAsync(x => x.SubscriptionId == s.Id && x.PeriodStart == start && x.Status != SubscriptionInvoiceStatus.Void, ct))
                throw new ConflictException("An invoice for that period already exists.");
            invoice = invoicing.IssueForPeriod(s, plan, settings, start, today, due);
        }
        log.Add("Invoice issued", cmd.OrgId, org.Name, $"{invoice.Number}: {invoice.Currency} {invoice.Total:N2}");
        await subscriptions.SaveChangesAsync(ct);
        await invoicing.NotifyAsync(invoice, s, settings, ct);
        return BillingHandlers.ToRow(invoice, today);
    }

    public async Task<TemporaryPassword> Handle(ResetClientUserPassword cmd, CancellationToken ct)
    {
        var org = await organizations.GetAsync(cmd.OrgId, ct);
        var member = (await directory.MembersAsync(cmd.OrgId, ct)).FirstOrDefault(m => m.UserId == cmd.UserId) ?? throw new NotFoundException();
        var password = await directory.IssueTemporaryPasswordAsync(cmd.OrgId, cmd.UserId, ct);
        await auth.RevokeSessionsAsync(cmd.UserId, null, "password-reset-by-platform", ct);
        log.Add("Temporary password issued", cmd.OrgId, org.Name, member.Email);
        await subscriptions.SaveChangesAsync(ct);
        return new(password);
    }

    public async Task<PagedResult<PlatformInvoiceRow>> Handle(ListPlatformInvoices q, CancellationToken ct)
    {
        var today = Today;
        var query = invoices.Query();
        if (q.OrgId is { } orgId) query = query.Where(i => i.OrgId == orgId);
        if (q.Status is { Length: > 0 } status)
        {
            if (status.Equals("overdue", StringComparison.OrdinalIgnoreCase)) query = query.Where(i => i.Status == SubscriptionInvoiceStatus.Open && i.DueOn < today);
            else if (Enum.TryParse<SubscriptionInvoiceStatus>(status, true, out var parsed)) query = query.Where(i => i.Status == parsed);
        }
        var names = await organizations.Query().ToDictionaryAsync(o => o.Id, o => o.Name, ct);
        IEnumerable<SubscriptionInvoice> rows = await query.OrderByDescending(i => i.IssuedOn).ThenByDescending(i => i.Number).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            rows = rows.Where(i => i.Number.Contains(s, StringComparison.OrdinalIgnoreCase) || names.GetValueOrDefault(i.OrgId, "").Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        var list = rows.ToList();
        return new(list.Skip((q.PageNumber - 1) * q.Size).Take(q.Size).Select(i => new PlatformInvoiceRow(
            i.Id, i.Number, i.Status, i.OrgId, names.GetValueOrDefault(i.OrgId, "Client"), i.Description, i.Total, i.Currency, i.IssuedOn, i.DueOn, i.PaidOn, i.PaymentMethod, i.PaymentReference,
            i.Status == SubscriptionInvoiceStatus.Open && i.DueOn < today)).ToList(), list.Count, q.PageNumber, q.Size);
    }

    private async Task<InvoiceDocument> DocumentAsync(SubscriptionInvoice i, CancellationToken ct)
        => BillingHandlers.ToDocument(i, await organizations.GetAsync(i.OrgId, ct), await subscriptions.Query().FirstOrDefaultAsync(s => s.OrgId == i.OrgId, ct), await settingsStore.GetAsync(ct));

    public async Task<InvoiceDocument> Handle(GetPlatformInvoice q, CancellationToken ct) => await DocumentAsync(await invoices.GetAsync(q.Id, ct), ct);

    public async Task<InvoiceDocument> Handle(RecordInvoicePayment cmd, CancellationToken ct)
    {
        var invoice = await invoices.GetForUpdateAsync(cmd.Id, ct);
        if (invoice.Status != SubscriptionInvoiceStatus.Open) throw new ConflictException("Only an open invoice can take a payment.");
        var today = Today;
        var paidOn = cmd.Input.PaidOn ?? today;
        if (paidOn > today) throw new FieldValidationException("paidOn", "The payment date cannot be in the future.");
        invoice.Status = SubscriptionInvoiceStatus.Paid;
        invoice.PaidOn = paidOn;
        invoice.PaymentMethod = cmd.Input.Method?.Trim();
        invoice.PaymentReference = cmd.Input.Reference?.Trim();
        var s = await subscriptions.Tracked().FirstOrDefaultAsync(x => x.OrgId == invoice.OrgId, ct);
        var org = await organizations.GetAsync(invoice.OrgId, ct);
        if (s is not null) SubscriptionRules.ApplyPayment(s, invoice, await HasOverdueAsync(invoice.OrgId, invoice.Id, ct), today);
        log.Add("Payment recorded", invoice.OrgId, org.Name, $"{invoice.Number}: {invoice.Currency} {invoice.Total:N2}{(invoice.PaymentMethod is { Length: > 0 } m ? $" by {m}" : "")}");
        await subscriptions.SaveChangesAsync(ct);
        access.Invalidate(invoice.OrgId);
        await mailer.SendAsync(s?.BillingEmail, $"Payment received for {invoice.Number}", $"Thank you. We received {invoice.Currency} {invoice.Total:N2} for invoice {invoice.Number}.", ct);
        return await DocumentAsync(invoice, ct);
    }

    public async Task<InvoiceDocument> Handle(VoidPlatformInvoice cmd, CancellationToken ct)
    {
        var invoice = await invoices.GetForUpdateAsync(cmd.Id, ct);
        if (invoice.Status != SubscriptionInvoiceStatus.Open) throw new ConflictException("Only an open invoice can be voided. Reopen a paid invoice first.");
        invoice.Status = SubscriptionInvoiceStatus.Void;
        invoice.VoidReason = cmd.Reason?.Trim();
        var s = await subscriptions.Tracked().FirstOrDefaultAsync(x => x.OrgId == invoice.OrgId, ct);
        var org = await organizations.GetAsync(invoice.OrgId, ct);
        if (s is { Status: SubscriptionStatus.PastDue } && !await HasOverdueAsync(invoice.OrgId, invoice.Id, ct)) { s.Status = SubscriptionStatus.Active; s.LapsedOn = null; s.LastReminderKey = null; }
        log.Add("Invoice voided", invoice.OrgId, org.Name, $"{invoice.Number}. {invoice.VoidReason}");
        await subscriptions.SaveChangesAsync(ct);
        access.Invalidate(invoice.OrgId);
        return await DocumentAsync(invoice, ct);
    }

    /// <summary>Takes back a payment recorded by mistake. The next daily run works out the client's state again.</summary>
    public async Task<InvoiceDocument> Handle(ReopenPlatformInvoice cmd, CancellationToken ct)
    {
        var invoice = await invoices.GetForUpdateAsync(cmd.Id, ct);
        if (invoice.Status != SubscriptionInvoiceStatus.Paid) throw new ConflictException("Only a paid invoice can be reopened.");
        invoice.Status = SubscriptionInvoiceStatus.Open;
        invoice.PaidOn = null; invoice.PaymentMethod = null; invoice.PaymentReference = null;
        var org = await organizations.GetAsync(invoice.OrgId, ct);
        log.Add("Payment taken back", invoice.OrgId, org.Name, invoice.Number);
        await subscriptions.SaveChangesAsync(ct);
        access.Invalidate(invoice.OrgId);
        return await DocumentAsync(invoice, ct);
    }

    public async Task<PagedResult<SignupRow>> Handle(ListSignups q, CancellationToken ct)
    {
        var query = signups.Query();
        if (q.Status is { Length: > 0 } status && Enum.TryParse<SignupStatus>(status, true, out var parsed)) query = query.Where(r => r.Status == parsed);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(r => r.BusinessName.Contains(s) || r.Email.Contains(s) || r.SignInKey.Contains(s));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAt).Skip((q.PageNumber - 1) * q.Size).Take(q.Size).ToListAsync(ct);
        var names = await plans.Query().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return new(rows.Select(r => ToRow(r, names.GetValueOrDefault(r.PlanId, "Plan"))).ToList(), total, q.PageNumber, q.Size);
    }

    private static SignupRow ToRow(SignupRequest r, string planName) => new(
        r.Id, r.Status, r.BusinessName, r.SignInKey, r.Industry, r.CountryCode, r.BaseCurrency, r.ContactName, r.Email, r.Phone, r.PlanId, planName, r.Cycle, r.EmailVerifiedAt, r.CreatedAt, r.ReviewedAt, r.RejectReason, r.ProvisionedOrgId);

    public async Task<SignupRow> Handle(ApproveSignup cmd, CancellationToken ct)
    {
        var r = await signups.GetForUpdateAsync(cmd.Id, ct);
        if (r.Status is not (SignupStatus.PendingReview or SignupStatus.PendingEmail)) throw new ConflictException("This registration has already been handled.");
        var result = await provisioner.ProvisionAsync(r, ct);
        r.Status = SignupStatus.Provisioned;
        r.ProvisionedOrgId = result.OrgId;
        r.PasswordHash = null;
        r.VerificationTokenHash = null;
        r.ReviewedAt = clock.UtcNow;
        r.ReviewedBy = user.UserId;
        log.Add("Registration approved", result.OrgId, r.BusinessName, r.Email);
        await signups.SaveChangesAsync(ct);
        return ToRow(r, (await plans.GetAsync(r.PlanId, ct)).Name);
    }

    public async Task<SignupRow> Handle(RejectSignup cmd, CancellationToken ct)
    {
        var r = await signups.GetForUpdateAsync(cmd.Id, ct);
        if (r.Status is not (SignupStatus.PendingReview or SignupStatus.PendingEmail)) throw new ConflictException("This registration has already been handled.");
        r.Status = SignupStatus.Rejected;
        r.RejectReason = cmd.Reason?.Trim();
        r.PasswordHash = null;
        r.VerificationTokenHash = null;
        r.ReviewedAt = clock.UtcNow;
        r.ReviewedBy = user.UserId;
        log.Add("Registration rejected", null, r.BusinessName, $"{r.Email}. {r.RejectReason}");
        await signups.SaveChangesAsync(ct);
        await mailer.SendAsync(r.Email, "About your registration", $"We could not set up {r.BusinessName} at this time.{(r.RejectReason is { Length: > 0 } reason ? $"\n\n{reason}" : "")}", ct);
        return ToRow(r, (await plans.GetAsync(r.PlanId, ct)).Name);
    }

    public async Task<PagedResult<ActivityRow>> Handle(ListPlatformActivity q, CancellationToken ct)
    {
        var query = activity.Query();
        if (q.OrgId is { } orgId) query = query.Where(a => a.OrgId == orgId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(a => a.Action.Contains(s) || a.Actor.Contains(s) || (a.ClientName != null && a.ClientName.Contains(s)) || (a.Detail != null && a.Detail.Contains(s)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.At).Skip((q.PageNumber - 1) * q.Size).Take(q.Size).ToListAsync(ct);
        return new(rows.Select(ToRow).ToList(), total, q.PageNumber, q.Size);
    }
}

internal sealed class PlatformSettingsHandlers(PlatformSettingsStore settingsStore, IRepository<PlatformSetting> settings, IReadRepository<SubscriptionPlan> plans, PlatformLog log, IPlatformMailer mailer, ISubscriptionLifecycle lifecycle, IPlatformDirectory directory, ICurrentUser user, IClock clock) :
    IRequestHandler<GetPlatformSettings, PlatformSettingsView>,
    IRequestHandler<UpdatePlatformSettings, PlatformSettingsView>,
    IRequestHandler<RunBillingNow, LifecycleResult>,
    IRequestHandler<ListPlatformTeam, IReadOnlyList<PlatformTeamMember>>,
    IRequestHandler<AddPlatformTeamMember, NewTeamMember>,
    IRequestHandler<RemovePlatformTeamMember, MediatR.Unit>
{
    private PlatformSettingsView ToView(PlatformSetting s) => new(
        s.SignupMode, s.RequireEmailVerification, mailer.IsEnabled, s.DefaultPlanId, s.Currency, s.TaxPercent, s.TaxLabel, s.InvoicePrefix, s.NextInvoiceNumber, s.InvoiceDueDays, s.RenewalLeadDays, s.GraceDays, s.SuspendAfterDays,
        s.CompanyName, s.CompanyAddress, s.CompanyTaxNumber, s.SupportEmail, s.TermsUrl, s.PaymentInstructions, s.TermsText ?? LegalDefaults.Terms, s.PrivacyText ?? LegalDefaults.Privacy, s.TermsText is null && s.PrivacyText is null, s.AboutText);

    public async Task<PlatformSettingsView> Handle(GetPlatformSettings q, CancellationToken ct) => ToView(await settingsStore.GetAsync(ct));

    public async Task<PlatformSettingsView> Handle(UpdatePlatformSettings cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (i.DefaultPlanId is { } planId && !await plans.AnyAsync(p => p.Id == planId && p.IsActive, ct)) throw new FieldValidationException("defaultPlanId", "Choose an active plan.");
        var s = await settingsStore.GetTrackedAsync(ct);
        var wasMode = s.SignupMode;
        s.SignupMode = i.SignupMode; s.RequireEmailVerification = i.RequireEmailVerification; s.DefaultPlanId = i.DefaultPlanId; s.Currency = i.Currency.ToUpperInvariant(); s.TaxPercent = i.TaxPercent; s.TaxLabel = i.TaxLabel.Trim();
        s.InvoicePrefix = i.InvoicePrefix.Trim().ToUpperInvariant(); s.InvoiceDueDays = i.InvoiceDueDays; s.RenewalLeadDays = i.RenewalLeadDays; s.GraceDays = i.GraceDays; s.SuspendAfterDays = i.SuspendAfterDays;
        s.CompanyName = (i.CompanyName ?? "").Trim(); s.CompanyAddress = i.CompanyAddress?.Trim(); s.CompanyTaxNumber = i.CompanyTaxNumber?.Trim(); s.SupportEmail = i.SupportEmail?.Trim(); s.TermsUrl = i.TermsUrl?.Trim(); s.PaymentInstructions = i.PaymentInstructions?.Trim();
        // Sending the starter wording back unchanged leaves it as the starter wording. Sending nothing (older callers) changes nothing.
        var terms = Clean(i.TermsText, LegalDefaults.Terms, s.TermsText);
        var privacy = Clean(i.PrivacyText, LegalDefaults.Privacy, s.PrivacyText);
        if (i.AboutText is not null) s.AboutText = string.IsNullOrWhiteSpace(i.AboutText) ? null : i.AboutText.Trim();
        var legalChanged = terms != s.TermsText || privacy != s.PrivacyText;
        s.TermsText = terms;
        s.PrivacyText = privacy;
        if (legalChanged) s.LegalUpdatedAt = clock.UtcNow;
        log.Add("Settings updated", null, null, string.Join(" ", new[] { wasMode == s.SignupMode ? null : $"Registration changed from {wasMode} to {s.SignupMode}.", legalChanged ? "Terms or privacy policy changed." : null }.Where(x => x is not null)) is { Length: > 0 } note ? note : null);
        await settings.SaveChangesAsync(ct);
        return ToView(s);
    }

    private static string? Clean(string? sent, string starter, string? current)
    {
        if (sent is null) return current;
        var text = sent.Replace("\r\n", "\n").Trim();
        return text.Length == 0 || text == starter.Replace("\r\n", "\n").Trim() ? null : text;
    }

    public async Task<LifecycleResult> Handle(RunBillingNow cmd, CancellationToken ct)
    {
        var result = await lifecycle.RunAsync(DateOnly.FromDateTime(clock.UtcNow), ct);
        log.Add("Billing run", null, null, $"{result.TrialsExpired} trials ended, {result.InvoicesIssued} invoices issued, {result.MarkedPastDue} overdue, {result.Suspended} suspended.");
        await settings.SaveChangesAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<PlatformTeamMember>> Handle(ListPlatformTeam q, CancellationToken ct) => await directory.TeamAsync(ct);

    public async Task<NewTeamMember> Handle(AddPlatformTeamMember cmd, CancellationToken ct)
    {
        var added = await directory.AddTeamMemberAsync(cmd.Input.Email.Trim(), cmd.Input.FullName.Trim(), ct);
        log.Add("Team member added", null, null, added.Member.Email);
        await settings.SaveChangesAsync(ct);
        return added;
    }

    public async Task<MediatR.Unit> Handle(RemovePlatformTeamMember cmd, CancellationToken ct)
    {
        var email = (await directory.TeamAsync(ct)).FirstOrDefault(m => m.UserId == cmd.UserId)?.Email;
        await directory.RemoveTeamMemberAsync(cmd.UserId, user.UserId ?? throw new ForbiddenException(), ct);
        log.Add("Team member removed", null, null, email);
        await settings.SaveChangesAsync(ct);
        return MediatR.Unit.Value;
    }
}
