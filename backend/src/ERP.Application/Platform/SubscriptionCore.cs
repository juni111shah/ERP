using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Platform;

/// <summary>How one workspace stands right now; drives the gate, the banner and the billing page.</summary>
public sealed record SubscriptionSummary(
    bool HasSubscription, SubscriptionStatus? Status, SubscriptionAccessLevel Access, string? PlanName, DateOnly? TrialEndsOn,
    int? TrialDaysLeft, DateOnly? CurrentPeriodEnd, DateOnly? LapsedOn, bool IsComplimentary, string? SuspendedReason)
{
    public static readonly SubscriptionSummary Unrestricted = new(false, null, SubscriptionAccessLevel.Full, null, null, null, null, null, false, null);
}

public static class SubscriptionProblems
{
    public const string ReadOnlyType = "subscription-read-only";
    public const string BlockedType = "subscription-blocked";
}

/// <summary>Cached look-up used on every request (implemented in Infrastructure).</summary>
public interface ISubscriptionAccess
{
    Task<SubscriptionSummary> GetAsync(Guid orgId, CancellationToken ct = default);
    void Invalidate(Guid orgId);
}

/// <summary>Plan limits for the caller's own workspace. A workspace with no subscription has none.</summary>
public interface ISubscriptionLimits
{
    Task EnsureUserSlotAsync(CancellationToken ct = default);
    Task EnsureLegalEntitySlotAsync(CancellationToken ct = default);
    Task EnsureModuleAllowedAsync(string moduleKey, CancellationToken ct = default);
}

/// <summary>Sends platform email. Skipped quietly when email is off; a failure is logged and never stops the work.</summary>
public interface IPlatformMailer
{
    bool IsEnabled { get; }
    string Link(string path);
    Task SendAsync(string? to, string subject, string body, CancellationToken ct = default);
}

public sealed record WorkspaceUsage(Guid OrgId, int ActiveUsers, int LegalEntities, IReadOnlyList<string> EnabledModules, DateTime? LastSignInAt);
public sealed record WorkspaceMemberView(Guid UserId, string Email, string FullName, bool IsActive, bool IsAdministrator, DateTime? LastSignInAt, bool TwoFactorEnabled);
/// <summary>
/// What a member of the platform team may do in the founder's console. Everyone on the team can read everything and do the day-to-day
/// work (notes, trials, invoices and payments, leads); a <see cref="Manager"/> can also make the changes that hurt when they go wrong
/// (suspend, cancel, plans, modules, voids, password resets, sign-ups, settings, the team). The <see cref="Owner"/> is the one account
/// named by Platform:FounderEmail: everything a manager can, plus the AI assistant and who is a manager. Stored values are Support and
/// Manager; Owner is worked out from the platform settings.
/// </summary>
public enum PlatformTeamRole { Support = 0, Manager = 1, Owner = 2 }

/// <summary>The console's authorization policies on top of "platform-admin" (on the team at all), checked against the account on every request.</summary>
public static class PlatformRights
{
    public const string ManagerPolicy = "platform-manager";
    public const string OwnerPolicy = "platform-owner";

    public static bool CanManage(PlatformTeamRole? role) => role is PlatformTeamRole.Manager or PlatformTeamRole.Owner;
}

public sealed record PlatformTeamMember(Guid UserId, string Email, string FullName, DateTime? LastSignInAt, bool TwoFactorEnabled, PlatformTeamRole Role = PlatformTeamRole.Support);
public sealed record NewTeamMember(PlatformTeamMember Member, string? TemporaryPassword);

/// <summary>Reads and account changes that cross workspaces. The one trusted route for the platform console (implemented in Infrastructure).</summary>
public interface IPlatformDirectory
{
    Task<bool> IsPlatformAdminAsync(Guid userId, CancellationToken ct = default);
    /// <summary>The one account that may manage AI services and their keys.</summary>
    Task<bool> IsPlatformOwnerAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Their role on the platform team, or null when they are not on it (or their account is switched off).</summary>
    Task<PlatformTeamRole?> PlatformRoleAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, WorkspaceUsage>> UsageAsync(IReadOnlyCollection<Guid>? orgIds = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkspaceMemberView>> MembersAsync(Guid orgId, CancellationToken ct = default);
    /// <summary>A registration's password: the hash for a new account, or null when the email has an account and this is its password. Throws a field error otherwise.</summary>
    Task<string?> PrepareSignupPasswordAsync(string email, string password, CancellationToken ct = default);
    string NewTemporaryPassword();
    Task<string> IssueTemporaryPasswordAsync(Guid orgId, Guid userId, CancellationToken ct = default);
    /// <summary>Switches off the workspace's modules a plan does not include and says how many there were. Their data stays.</summary>
    Task<int> DisableModulesOutsideAsync(Guid orgId, IReadOnlyCollection<string> allowed, CancellationToken ct = default);
    Task<IReadOnlyList<PlatformTeamMember>> TeamAsync(CancellationToken ct = default);
    Task<NewTeamMember> AddTeamMemberAsync(string email, string fullName, PlatformTeamRole role, CancellationToken ct = default);
    Task RemoveTeamMemberAsync(Guid userId, Guid actingUserId, CancellationToken ct = default);
    /// <summary>Makes a team member a manager or support again. The owner's role is fixed.</summary>
    Task SetTeamRoleAsync(Guid userId, PlatformTeamRole role, CancellationToken ct = default);
}

public interface ISubscriptionLifecycle
{
    /// <summary>The daily run: ends trials, issues renewals, marks overdue, suspends, sends reminders. Safe to run repeatedly.</summary>
    Task<LifecycleResult> RunAsync(DateOnly today, CancellationToken ct = default);
}

public sealed record LifecycleResult(int TrialsExpired, int InvoicesIssued, int MarkedPastDue, int Suspended, int RemindersSent, int SignupsExpired);

/// <summary>The billing rules, free of any storage, so they can be read and tested on their own.</summary>
public static class SubscriptionRules
{
    public static SubscriptionAccessLevel AccessOf(WorkspaceSubscription s, PlatformSetting settings, DateOnly today) => s.Status switch
    {
        SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled => SubscriptionAccessLevel.Blocked,
        _ when s.IsComplimentary => SubscriptionAccessLevel.Full,
        SubscriptionStatus.Expired => SubscriptionAccessLevel.ReadOnly,
        // The daily run may not have reached it yet; the date decides.
        SubscriptionStatus.Trialing when s.TrialEndsOn is { } end && end < today => SubscriptionAccessLevel.ReadOnly,
        SubscriptionStatus.PastDue when s.LapsedOn is { } lapsed && today > lapsed.AddDays(settings.GraceDays) => SubscriptionAccessLevel.ReadOnly,
        _ => SubscriptionAccessLevel.Full,
    };

    public static decimal CyclePrice(WorkspaceSubscription s, SubscriptionPlan plan)
        => s.IsComplimentary ? 0 : s.CustomPrice ?? (s.Cycle == BillingCycle.Annual ? plan.AnnualPrice : plan.MonthlyPrice);

    /// <summary>What one subscription is worth per month, for monthly recurring revenue.</summary>
    public static decimal MonthlyValue(WorkspaceSubscription s, SubscriptionPlan plan)
        => s.Cycle == BillingCycle.Annual ? Math.Round(CyclePrice(s, plan) / 12, 2) : CyclePrice(s, plan);

    /// <summary>A status in the words of the activity log.</summary>
    public static string StatusWords(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Trialing => "on trial",
        SubscriptionStatus.PastDue => "overdue",
        _ => status.ToString().ToLowerInvariant(),
    };

    public static DateOnly PeriodEnd(DateOnly start, BillingCycle cycle)
        => (cycle == BillingCycle.Annual ? start.AddYears(1) : start.AddMonths(1)).AddDays(-1);

    /// <summary>A new subscription: a trial when the plan has one, otherwise a running paid or free period.</summary>
    public static WorkspaceSubscription Start(SubscriptionPlan plan, BillingCycle cycle, DateOnly today, ClientStartMode mode, int? trialDays = null)
    {
        var s = new WorkspaceSubscription { PlanId = plan.Id, Cycle = cycle, StartedOn = today };
        var days = trialDays ?? plan.TrialDays;
        if (mode == ClientStartMode.Complimentary) { s.IsComplimentary = true; s.Status = SubscriptionStatus.Active; }
        else if (mode == ClientStartMode.Trial && days > 0) { s.Status = SubscriptionStatus.Trialing; s.TrialEndsOn = today.AddDays(days); }
        else { s.Status = SubscriptionStatus.Active; s.CurrentPeriodStart = today; s.CurrentPeriodEnd = PeriodEnd(today, cycle); }
        return s;
    }

    /// <summary>
    /// A recorded payment brings the workspace back when it clears what was overdue. It never undoes the founder's own
    /// suspension or a cancellation: those need an explicit reactivation.
    /// </summary>
    public static void ApplyPayment(WorkspaceSubscription s, SubscriptionInvoice invoice, bool otherOverdue, DateOnly today)
    {
        if (invoice.PeriodEnd is { } end && (s.CurrentPeriodEnd is null || end > s.CurrentPeriodEnd))
        {
            s.CurrentPeriodStart = invoice.PeriodStart ?? today;
            s.CurrentPeriodEnd = end;
        }
        if (s.Status == SubscriptionStatus.Cancelled || (s.Status == SubscriptionStatus.Suspended && s.SuspendedByFounder) || otherOverdue) return;
        if (s.CurrentPeriodEnd is { } covered && covered >= today)
        {
            s.Status = SubscriptionStatus.Active;
            s.LapsedOn = null;
            s.SuspendedAt = null;
            s.SuspendedReason = null;
            s.LastReminderKey = null;
        }
    }

    /// <summary>What state a suspended or cancelled workspace returns to.</summary>
    public static void Reactivate(WorkspaceSubscription s, bool hasOverdue, DateOnly today)
    {
        s.SuspendedAt = null; s.SuspendedReason = null; s.SuspendedByFounder = false;
        s.CancelledAt = null; s.CancelReason = null;
        s.LapsedOn = null; s.LastReminderKey = null;
        if (s.IsComplimentary) s.Status = SubscriptionStatus.Active;
        else if (hasOverdue) { s.Status = SubscriptionStatus.PastDue; s.LapsedOn = today; }
        else if (s.CurrentPeriodEnd is { } end && end >= today) s.Status = SubscriptionStatus.Active;
        else if (s.CurrentPeriodEnd is null && s.TrialEndsOn is { } trial && trial >= today) s.Status = SubscriptionStatus.Trialing;
        else { s.Status = SubscriptionStatus.Expired; s.LapsedOn = today; }
    }
}

public enum ClientStartMode { Trial, Active, Complimentary }

/// <summary>Limits a workspace by its plan. Runs inside the caller's workspace, so the counts are its own.</summary>
internal sealed class SubscriptionLimits(
    ICurrentUser user, IReadRepository<WorkspaceSubscription> subscriptions, IReadRepository<SubscriptionPlan> plans,
    IReadRepository<OrganizationMember> members, IReadRepository<LegalEntity> legalEntities, IOrgContext org) : ISubscriptionLimits
{
    private async Task<SubscriptionPlan?> PlanAsync(CancellationToken ct)
    {
        var orgId = org.OrgId ?? user.OrgId;
        if (orgId is null) return null;
        var planId = await subscriptions.Query().Where(s => s.OrgId == orgId).Select(s => (Guid?)s.PlanId).FirstOrDefaultAsync(ct);
        return planId is null ? null : await plans.FindAsync(planId.Value, ct);
    }

    public async Task EnsureUserSlotAsync(CancellationToken ct = default)
    {
        if (await PlanAsync(ct) is not { MaxUsers: { } max } plan) return;
        if (await members.CountAsync(m => m.IsActive, ct) >= max)
            throw new ConflictException($"Your {plan.Name} plan includes {max} active users. Upgrade the plan or deactivate a user first.");
    }

    public async Task EnsureLegalEntitySlotAsync(CancellationToken ct = default)
    {
        if (await PlanAsync(ct) is not { MaxLegalEntities: { } max } plan) return;
        if (await legalEntities.CountAsync(null, ct) >= max)
            throw new ConflictException($"Your {plan.Name} plan includes {max} legal entities. Upgrade the plan to add another.");
    }

    public async Task EnsureModuleAllowedAsync(string moduleKey, CancellationToken ct = default)
    {
        if (await PlanAsync(ct) is not { } plan) return;
        var allowed = plan.ModuleList();
        if (allowed.Count > 0 && !allowed.Contains(moduleKey, StringComparer.OrdinalIgnoreCase))
            throw new ConflictException($"This module is not part of your {plan.Name} plan. Upgrade the plan to use it.");
    }
}

/// <summary>The founder's activity log.</summary>
internal sealed class PlatformLog(IRepository<PlatformActivity> activity, ICurrentUser user, IClock clock)
{
    public void Add(string action, Guid? orgId, string? clientName, string? detail = null) => activity.Add(new PlatformActivity
    {
        At = clock.UtcNow, ActorUserId = user.UserId, Actor = user.Email ?? "system", Action = action, OrgId = orgId, ClientName = clientName,
        Detail = detail is { Length: > 1000 } ? detail[..1000] : detail,
    });
}

/// <summary>Reads the one settings row, creating it the first time.</summary>
internal sealed class PlatformSettingsStore(IRepository<PlatformSetting> settings)
{
    public async Task<PlatformSetting> GetTrackedAsync(CancellationToken ct)
    {
        var row = await settings.Tracked().OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (row is not null) return row;
        row = new PlatformSetting();
        settings.Add(row);
        return row;
    }

    public async Task<PlatformSetting> GetAsync(CancellationToken ct)
        => await settings.Query().OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct) ?? new PlatformSetting();
}

/// <summary>Issues client invoices and tells the client about them.</summary>
internal sealed class SubscriptionInvoicing(IRepository<SubscriptionInvoice> invoices, IPlatformMailer mailer)
{
    public SubscriptionInvoice Issue(
        WorkspaceSubscription s, PlatformSetting settings, decimal amount, string description, DateOnly? periodStart, DateOnly? periodEnd, DateOnly issuedOn, DateOnly dueOn)
    {
        var tax = Math.Round(amount * settings.TaxPercent / 100, 2, MidpointRounding.AwayFromZero);
        var invoice = new SubscriptionInvoice
        {
            Number = $"{settings.InvoicePrefix}-{settings.NextInvoiceNumber:000000}", OrgId = s.OrgId, SubscriptionId = s.Id, Status = SubscriptionInvoiceStatus.Open,
            Description = description, PeriodStart = periodStart, PeriodEnd = periodEnd, Subtotal = amount, TaxPercent = settings.TaxPercent, TaxLabel = settings.TaxLabel, TaxAmount = tax,
            Total = amount + tax, Currency = settings.Currency, IssuedOn = issuedOn, DueOn = dueOn,
        };
        settings.NextInvoiceNumber++;
        invoices.Add(invoice);
        return invoice;
    }

    /// <summary>The invoice for one billing cycle of the plan.</summary>
    public SubscriptionInvoice IssueForPeriod(WorkspaceSubscription s, SubscriptionPlan plan, PlatformSetting settings, DateOnly start, DateOnly issuedOn, DateOnly dueOn)
    {
        var end = SubscriptionRules.PeriodEnd(start, s.Cycle);
        return Issue(s, settings, SubscriptionRules.CyclePrice(s, plan), $"{plan.Name} plan, {(s.Cycle == BillingCycle.Annual ? "annual" : "monthly")}: {start:d MMM yyyy} to {end:d MMM yyyy}", start, end, issuedOn, dueOn);
    }

    public Task NotifyAsync(SubscriptionInvoice i, WorkspaceSubscription s, PlatformSetting settings, CancellationToken ct) => mailer.SendAsync(s.BillingEmail,
        $"Invoice {i.Number} from {CompanyOf(settings)}",
        $"Invoice {i.Number} for {i.Currency} {i.Total:N2} is due on {i.DueOn:d MMM yyyy}.\n\n{i.Description}\n\n{settings.PaymentInstructions}\n\nView it here: {mailer.Link("/admin/billing")}", ct);

    public static string CompanyOf(PlatformSetting settings) => string.IsNullOrWhiteSpace(settings.CompanyName) ? "ERP" : settings.CompanyName;
}

/// <summary>The daily billing run. Every step checks the current state first, so running it twice changes nothing.</summary>
internal sealed class SubscriptionLifecycle(
    IRepository<WorkspaceSubscription> subscriptions, IRepository<SubscriptionPlan> plans, IRepository<SubscriptionInvoice> invoices, IRepository<SignupRequest> signups,
    IReadRepository<Organization> organizations, PlatformSettingsStore settingsStore, SubscriptionInvoicing invoicing, PlatformLog log, IPlatformMailer mailer,
    SubscriptionChanges changes, IClock clock) : ISubscriptionLifecycle
{
    private static readonly int[] TrialReminderDays = [7, 3, 1];

    public async Task<LifecycleResult> RunAsync(DateOnly today, CancellationToken ct = default)
    {
        var settings = await settingsStore.GetTrackedAsync(ct);
        var running = await subscriptions.Tracked().Where(s => s.Status != SubscriptionStatus.Cancelled && s.Status != SubscriptionStatus.Suspended && !s.IsComplimentary).ToListAsync(ct);
        var planById = await plans.Query().ToDictionaryAsync(p => p.Id, ct);
        var ids = running.Select(s => s.OrgId).ToList();
        var names = await organizations.Query().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name, ct);
        var openInvoices = await invoices.Query().Where(i => i.Status == SubscriptionInvoiceStatus.Open && ids.Contains(i.OrgId)).ToListAsync(ct);
        var mail = new List<Func<Task>>();
        var changed = new HashSet<Guid>();
        int expired = 0, issued = 0, pastDue = 0, suspended = 0, reminders = 0;

        foreach (var s in running)
        {
            if (!planById.TryGetValue(s.PlanId, out var plan)) continue;
            var name = names.GetValueOrDefault(s.OrgId, "Client");
            var before = s.Status;
            var overdue = openInvoices.Where(i => i.OrgId == s.OrgId && i.DueOn < today).OrderBy(i => i.DueOn).FirstOrDefault();

            switch (s.Status)
            {
                case SubscriptionStatus.Trialing when s.TrialEndsOn is { } end && end < today:
                    s.Status = SubscriptionStatus.Expired;
                    s.LapsedOn = end.AddDays(1);
                    expired++;
                    log.Add("Trial ended", s.OrgId, name, $"The trial ended on {end:d MMM yyyy}.");
                    mail.Add(() => mailer.SendAsync(s.BillingEmail, "Your trial has ended",
                        $"The trial of {name} has ended. Your data is safe and can be viewed, but changes are paused until a plan is active.\n\nChoose a plan: {mailer.Link("/admin/billing")}", ct));
                    break;

                case SubscriptionStatus.Trialing when s.TrialEndsOn is { } end:
                    var left = end.DayNumber - today.DayNumber;
                    var key = $"trial-{left}";
                    if (TrialReminderDays.Contains(left) && s.LastReminderKey != key)
                    {
                        s.LastReminderKey = key;
                        reminders++;
                        mail.Add(() => mailer.SendAsync(s.BillingEmail, $"Your trial ends in {left} day{(left == 1 ? "" : "s")}",
                            $"The trial of {name} ends on {end:d MMM yyyy}. Choose a plan to keep working without a break: {mailer.Link("/admin/billing")}", ct));
                    }
                    break;

                case SubscriptionStatus.Expired when s.LapsedOn is { } lapsed && today >= lapsed.AddDays(settings.SuspendAfterDays):
                    Suspend(s, "Trial ended with no plan chosen.");
                    suspended++;
                    log.Add("Suspended", s.OrgId, name, s.SuspendedReason);
                    break;

                case SubscriptionStatus.Active:
                    s.CurrentPeriodStart ??= today;
                    s.CurrentPeriodEnd ??= SubscriptionRules.PeriodEnd(today, s.Cycle);
                    if (overdue is not null)
                    {
                        s.Status = SubscriptionStatus.PastDue;
                        s.LapsedOn = overdue.DueOn.AddDays(1);
                        pastDue++;
                        log.Add("Payment overdue", s.OrgId, name, $"Invoice {overdue.Number} was due on {overdue.DueOn:d MMM yyyy}.");
                        var i = overdue;
                        mail.Add(() => mailer.SendAsync(s.BillingEmail, $"Invoice {i.Number} is overdue",
                            $"Invoice {i.Number} for {i.Currency} {i.Total:N2} was due on {i.DueOn:d MMM yyyy}. Please pay within {settings.GraceDays} days to avoid changes being paused.\n\n{settings.PaymentInstructions}", ct));
                        break;
                    }
                    if (SubscriptionRules.CyclePrice(s, plan) <= 0)
                    {
                        // A free plan simply rolls on.
                        if (s.CurrentPeriodEnd < today) { s.CurrentPeriodStart = today; s.CurrentPeriodEnd = SubscriptionRules.PeriodEnd(today, s.Cycle); }
                        break;
                    }
                    var periodEnd = s.CurrentPeriodEnd.Value;
                    var next = periodEnd.AddDays(1);
                    if (today >= periodEnd.AddDays(-settings.RenewalLeadDays)
                        && !await invoices.AnyAsync(x => x.SubscriptionId == s.Id && x.PeriodStart == next && x.Status != SubscriptionInvoiceStatus.Void, ct))
                    {
                        var renewal = invoicing.IssueForPeriod(s, plan, settings, next, today, periodEnd < today ? today : periodEnd);
                        issued++;
                        log.Add("Renewal invoice issued", s.OrgId, name, $"{renewal.Number}: {renewal.Currency} {renewal.Total:N2}");
                        mail.Add(() => invoicing.NotifyAsync(renewal, s, settings, ct));
                    }
                    break;

                case SubscriptionStatus.PastDue:
                    if (overdue is null) { s.Status = SubscriptionStatus.Active; s.LapsedOn = null; s.LastReminderKey = null; break; }
                    var since = s.LapsedOn ?? today;
                    if (today >= since.AddDays(settings.SuspendAfterDays))
                    {
                        Suspend(s, $"Invoice {overdue.Number} is unpaid.");
                        suspended++;
                        log.Add("Suspended", s.OrgId, name, s.SuspendedReason);
                        mail.Add(() => mailer.SendAsync(s.BillingEmail, "Your workspace is suspended",
                            $"{name} is suspended because invoice {overdue.Number} is unpaid. Your data is kept. Pay the invoice or contact us to restore access.\n\n{settings.PaymentInstructions}", ct));
                    }
                    else if (today > since.AddDays(settings.GraceDays) && s.LastReminderKey != "read-only")
                    {
                        s.LastReminderKey = "read-only";
                        reminders++;
                        changed.Add(s.OrgId); // now view-only: the client's screens show it at once
                        mail.Add(() => mailer.SendAsync(s.BillingEmail, "Changes are paused on your workspace",
                            $"Invoice {overdue.Number} is still unpaid, so {name} is now view-only. Pay the invoice to continue working.\n\n{settings.PaymentInstructions}", ct));
                    }
                    break;
            }
            if (s.Status != before) changed.Add(s.OrgId);
        }

        var now = clock.UtcNow;
        var stale = await signups.Tracked().Where(r => r.Status == SignupStatus.PendingEmail && r.VerificationExpiresAt < now).ToListAsync(ct);
        foreach (var r in stale) { r.Status = SignupStatus.Expired; r.PasswordHash = null; r.VerificationTokenHash = null; }

        await subscriptions.SaveChangesAsync(ct);
        // Each client whose standing changed sees it at once: the blocked page, the view-only banner, or its normal workspace again.
        foreach (var orgId in changed) await changes.ChangedAsync(orgId, ct);
        foreach (var send in mail) await send();
        return new(expired, issued, pastDue, suspended, reminders, stale.Count);
    }

    private void Suspend(WorkspaceSubscription s, string reason)
    {
        s.Status = SubscriptionStatus.Suspended;
        s.SuspendedAt = clock.UtcNow;
        s.SuspendedReason = reason;
        s.SuspendedByFounder = false;
    }
}
