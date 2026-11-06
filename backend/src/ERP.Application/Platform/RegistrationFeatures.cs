using System.Security.Cryptography;
using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Platform;

public sealed record PublicPlan(
    Guid Id, string Code, string Name, string? Description, decimal MonthlyPrice, decimal AnnualPrice, string Currency, int TrialDays,
    int? MaxUsers, int? MaxLegalEntities, IReadOnlyList<string> Modules, IReadOnlyList<string> Features, bool IsRecommended);

public sealed record SignupConfig(SignupMode Mode, bool EmailVerification, Guid? DefaultPlanId, string Currency, string? SupportEmail, string? TermsUrl, IReadOnlyList<PublicPlan> Plans);

public sealed record SignupInput(
    string BusinessName, string SignInKey, string Industry, string CountryCode, string BaseCurrency, string? TimeZone,
    string ContactName, string Email, string? Phone, string Password, Guid? PlanId, BillingCycle? Cycle, bool AcceptTerms, string? Website);

public sealed record SignupResult(SignupStatus Status, string Message, string? SignInKey);
public sealed record SignInKeyAvailability(bool Available, string? Reason);

/// <summary>The two public documents, ready to show.</summary>
public sealed record LegalDocuments(string CompanyName, string? SupportEmail, string? TermsUrl, string Terms, string Privacy, DateTime? UpdatedAt);

public sealed record GetSignupConfig : IQuery<SignupConfig>;
public sealed record GetLegalDocuments : IQuery<LegalDocuments>;
public sealed record CheckSignInKey(string Key) : IQuery<SignInKeyAvailability>;
public sealed record RegisterWorkspace(SignupInput Input, string? Ip) : ICommand<SignupResult>;
public sealed record VerifySignup(string Token) : ICommand<SignupResult>;

public sealed class RegisterWorkspaceValidator : AbstractValidator<RegisterWorkspace>
{
    public RegisterWorkspaceValidator()
    {
        RuleFor(x => x.Input.BusinessName).NotEmpty().WithMessage("Enter the business name.").MaximumLength(200);
        RuleFor(x => x.Input.SignInKey).NotEmpty().WithMessage("Choose a workspace code.").Matches("^[a-zA-Z0-9][a-zA-Z0-9-]{1,48}$").WithMessage("Use 2-49 letters, digits or dashes.");
        RuleFor(x => x.Input.Industry).NotEmpty().WithMessage("Choose an industry.");
        RuleFor(x => x.Input.CountryCode).Length(2).WithMessage("Choose a country.");
        RuleFor(x => x.Input.BaseCurrency).Length(3).WithMessage("Choose a currency.");
        RuleFor(x => x.Input.ContactName).NotEmpty().WithMessage("Enter your name.").MaximumLength(200);
        RuleFor(x => x.Input.Email).NotEmpty().WithMessage("Enter your work email.").EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(256);
        RuleFor(x => x.Input.Phone).MaximumLength(40);
        RuleFor(x => x.Input.Password).NotEmpty().WithMessage("Choose a password.");
        RuleFor(x => x.Input.AcceptTerms).Equal(true).WithMessage("Please accept the terms to continue.");
    }
}

/// <summary>Turns an accepted registration into a working client: workspace, first administrator and subscription, all in one transaction.</summary>
internal sealed class ClientProvisioner(IOnboardingService onboarding, SubscriptionInvoicing invoicing, PlatformSettingsStore settingsStore, IRepository<SubscriptionPlan> plans, IPlatformMailer mailer, IClock clock)
{
    public async Task<OnboardingResult> ProvisionAsync(SignupRequest r, CancellationToken ct)
    {
        var plan = await plans.GetAsync(r.PlanId, ct);
        var request = new OnboardingRequest(r.BusinessName, r.SignInKey, r.Industry, r.CountryCode, r.BaseCurrency, r.TimeZone, r.BusinessName, r.Email, r.ContactName, "");
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var subscription = SubscriptionRules.Start(plan, r.Cycle, today, ClientStartMode.Trial);
        subscription.BillingEmail = r.Email;
        subscription.ContactPhone = r.Phone;
        var result = await onboarding.CreateWorkspaceAsync(request, new OnboardingOptions(r.PasswordHash, TrustExistingAccount: r.PasswordHash is null, Subscription: subscription, AllowedModules: plan.ModuleList()), ct);
        await IssueFirstInvoiceAsync(subscription, plan, today, ct);

        await mailer.SendAsync(r.Email, "Your workspace is ready",
            $"{r.BusinessName} is ready. Sign in with this email and the password you chose:\n{mailer.Link("/auth/login")}\n\nWorkspace code: {r.SignInKey}", ct);
        return result;
    }

    /// <summary>A paid plan with no trial starts running straight away, so its first invoice goes out with it.</summary>
    public async Task IssueFirstInvoiceAsync(WorkspaceSubscription subscription, SubscriptionPlan plan, DateOnly today, CancellationToken ct)
    {
        if (subscription.Status != SubscriptionStatus.Active || subscription.IsComplimentary || SubscriptionRules.CyclePrice(subscription, plan) <= 0) return;
        var settings = await settingsStore.GetTrackedAsync(ct);
        var invoice = invoicing.IssueForPeriod(subscription, plan, settings, today, today, today.AddDays(settings.InvoiceDueDays));
        await plans.SaveChangesAsync(ct);
        await invoicing.NotifyAsync(invoice, subscription, settings, ct);
    }
}

internal sealed class RegistrationHandlers(
    IRepository<SignupRequest> signups, IReadRepository<SubscriptionPlan> plans, IReadRepository<Organization> organizations, IOnboardingService onboarding,
    IPlatformDirectory directory, PlatformSettingsStore settingsStore, ClientProvisioner provisioner, PlatformLog log, IPlatformMailer mailer, IClock clock) :
    IRequestHandler<GetSignupConfig, SignupConfig>,
    IRequestHandler<GetLegalDocuments, LegalDocuments>,
    IRequestHandler<CheckSignInKey, SignInKeyAvailability>,
    IRequestHandler<RegisterWorkspace, SignupResult>,
    IRequestHandler<VerifySignup, SignupResult>
{
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
        { "admin", "api", "app", "auth", "billing", "founder", "help", "hq", "mail", "platform", "platform-hq", "portal", "root", "support", "system", "www" };
    private const int VerificationHours = 48;
    private const string ReviewMessage = "Thanks. We have your registration and will email you as soon as your workspace is ready.";

    private bool Verifies(PlatformSetting s) => s.RequireEmailVerification && mailer.IsEnabled;

    public async Task<SignupConfig> Handle(GetSignupConfig q, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var list = await plans.Query().Where(p => p.IsActive && p.IsPublic).OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
        return new(settings.SignupMode, Verifies(settings), settings.DefaultPlanId, settings.Currency, settings.SupportEmail, settings.TermsUrl,
            list.Select(p => new PublicPlan(p.Id, p.Code, p.Name, p.Description, p.MonthlyPrice, p.AnnualPrice, settings.Currency, p.TrialDays, p.MaxUsers, p.MaxLegalEntities, p.ModuleList(), p.FeatureList(), p.IsRecommended)).ToList());
    }

    public async Task<LegalDocuments> Handle(GetLegalDocuments q, CancellationToken ct)
    {
        var s = await settingsStore.GetAsync(ct);
        return new(s.CompanyName, s.SupportEmail, s.TermsUrl,
            LegalDefaults.Fill(s.TermsText ?? LegalDefaults.Terms, s.CompanyName, s.SupportEmail),
            LegalDefaults.Fill(s.PrivacyText ?? LegalDefaults.Privacy, s.CompanyName, s.SupportEmail), s.LegalUpdatedAt ?? s.CreatedAt);
    }

    public async Task<SignInKeyAvailability> Handle(CheckSignInKey q, CancellationToken ct)
    {
        var key = (q.Key ?? "").Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z0-9][a-z0-9-]{1,48}$")) return new(false, "Use 2-49 letters, digits or dashes.");
        return await KeyTakenAsync(key, null, ct) ? new(false, "That code is taken. Try another.") : new(true, null);
    }

    private async Task<bool> KeyTakenAsync(string key, string? ownEmail, CancellationToken ct)
        => ReservedKeys.Contains(key)
           || await organizations.AnyAsync(o => o.Code == key, ct)
           || await signups.AnyAsync(r => r.SignInKey == key && r.Email != ownEmail && (r.Status == SignupStatus.PendingEmail || r.Status == SignupStatus.PendingReview), ct);

    public async Task<SignupResult> Handle(RegisterWorkspace cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var settings = await settingsStore.GetAsync(ct);
        if (settings.SignupMode == SignupMode.Closed)
            throw new ForbiddenException(settings.SupportEmail is { Length: > 0 } mail ? $"Registration is closed right now. Contact {mail} to get started." : "Registration is closed right now.");
        // The hidden field only a bot fills in: answer as usual and keep nothing.
        if (!string.IsNullOrWhiteSpace(i.Website)) return new(SignupStatus.PendingReview, ReviewMessage, null);

        var key = i.SignInKey.Trim().ToLowerInvariant();
        var email = i.Email.Trim();
        if (onboarding.IndustryViews().All(v => v.Key != i.Industry)) throw new FieldValidationException("industry", "Choose an industry from the list.");
        var planId = i.PlanId ?? settings.DefaultPlanId;
        var plan = planId is null ? null : await plans.Query().FirstOrDefaultAsync(p => p.Id == planId && p.IsActive && p.IsPublic, ct);
        if (plan is null) throw new FieldValidationException("planId", "Choose a plan.");
        if (await KeyTakenAsync(key, email, ct)) throw new FieldValidationException("signInKey", "That code is taken. Try another.");

        var hash = await directory.PrepareSignupPasswordAsync(email, i.Password, ct);

        // Registering again replaces an earlier one that is still waiting (a lost email, a typo).
        var earlier = await signups.Tracked().Where(r => r.Email == email && (r.Status == SignupStatus.PendingEmail || r.Status == SignupStatus.PendingReview)).ToListAsync(ct);
        foreach (var e in earlier) { e.Status = SignupStatus.Expired; e.PasswordHash = null; e.VerificationTokenHash = null; }

        var request = new SignupRequest
        {
            BusinessName = i.BusinessName.Trim(), SignInKey = key, Industry = i.Industry, CountryCode = i.CountryCode.ToUpperInvariant(), BaseCurrency = i.BaseCurrency.ToUpperInvariant(),
            TimeZone = i.TimeZone, ContactName = i.ContactName.Trim(), Email = email, Phone = i.Phone?.Trim(), PasswordHash = hash, PlanId = plan.Id, Cycle = i.Cycle ?? BillingCycle.Monthly, CreatedIp = cmd.Ip, TermsAcceptedAt = clock.UtcNow,
        };
        signups.Add(request);

        string? token = null;
        if (Verifies(settings))
        {
            token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            request.Status = SignupStatus.PendingEmail;
            request.VerificationTokenHash = HashToken(token);
            request.VerificationExpiresAt = clock.UtcNow.AddHours(VerificationHours);
        }
        else
        {
            // Waits for a person, or is provisioned just below. Asked to verify with no email server to do it: a person
            // checks instead of letting it through unverified.
            request.Status = SignupStatus.PendingReview;
        }
        log.Add("Registration received", null, request.BusinessName, $"{email}, {plan.Name} plan");
        await signups.SaveChangesAsync(ct);

        if (token is not null)
        {
            await mailer.SendAsync(email, "Confirm your email",
                $"Please confirm your email to continue setting up {request.BusinessName}:\n{mailer.Link($"/register/verify?token={Uri.EscapeDataString(token)}")}\n\nThis link works for {VerificationHours} hours. If this was not you, ignore this email.", ct);
            return new(SignupStatus.PendingEmail, $"We sent a link to {email}. Open it to confirm your email.", null);
        }
        if (settings.SignupMode == SignupMode.Open && !settings.RequireEmailVerification) return await ProvisionAsync(request, ct);

        await NotifyFounderAsync(settings, request, ct);
        return new(SignupStatus.PendingReview, ReviewMessage, null);
    }

    public async Task<SignupResult> Handle(VerifySignup cmd, CancellationToken ct)
    {
        var hash = HashToken(cmd.Token ?? "");
        var request = await signups.Tracked().FirstOrDefaultAsync(r => r.VerificationTokenHash == hash && r.Status == SignupStatus.PendingEmail, ct)
            ?? throw new ConflictException("This link has already been used or is no longer valid.");
        if (request.VerificationExpiresAt < clock.UtcNow)
        {
            request.Status = SignupStatus.Expired;
            request.PasswordHash = null;
            request.VerificationTokenHash = null;
            await signups.SaveChangesAsync(ct);
            throw new ConflictException("This link has expired. Please register again.");
        }
        var settings = await settingsStore.GetAsync(ct);
        request.EmailVerifiedAt = clock.UtcNow;
        request.VerificationTokenHash = null;
        request.Status = SignupStatus.PendingReview;
        await signups.SaveChangesAsync(ct);
        if (settings.SignupMode == SignupMode.Open) return await ProvisionAsync(request, ct);
        await NotifyFounderAsync(settings, request, ct);
        return new(SignupStatus.PendingReview, ReviewMessage, null);
    }

    private async Task<SignupResult> ProvisionAsync(SignupRequest request, CancellationToken ct)
    {
        var result = await provisioner.ProvisionAsync(request, ct);
        request.Status = SignupStatus.Provisioned;
        request.ProvisionedOrgId = result.OrgId;
        request.PasswordHash = null;
        log.Add("Client created", result.OrgId, request.BusinessName, "From the public registration page.");
        await signups.SaveChangesAsync(ct);
        return new(SignupStatus.Provisioned, "Your workspace is ready. Sign in to start.", request.SignInKey);
    }

    private Task NotifyFounderAsync(PlatformSetting settings, SignupRequest request, CancellationToken ct)
        => mailer.SendAsync(settings.SupportEmail, $"New registration: {request.BusinessName}", $"{request.BusinessName} ({request.Email}) registered and is waiting for approval.\n\n{mailer.Link("/platform/signups")}", ct);

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
