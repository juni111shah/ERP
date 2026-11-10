using System.Security.Cryptography;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class RegisterWorkspaceHandler(
    ISignupRequestRepository signups, ISubscriptionPlanRepository plans, IOnboardingService onboarding, IPlatformDirectory directory, PlatformSettingsStore settingsStore,
    SignupWorkflow workflow, PlatformLog log, IPlatformMailer mailer, IClock clock) : ICommandHandler<RegisterWorkspaceCommand, SignupResult>
{
    public async Task<SignupResult> Handle(RegisterWorkspaceCommand command, CancellationToken ct)
    {
        // 1. Registration must be open; a bot gets the usual answer and nothing is kept.
        var i = command.Input;
        var settings = await settingsStore.GetAsync(ct);
        if (settings.SignupMode == SignupMode.Closed)
            throw new ForbiddenException(settings.SupportEmail is { Length: > 0 } mail ? $"Registration is closed right now. Contact {mail} to get started." : "Registration is closed right now.");
        // The hidden field only a bot fills in: answer as usual and keep nothing.
        if (!string.IsNullOrWhiteSpace(i.Website)) return new(SignupStatus.PendingReview, SignupWorkflow.ReviewMessage, null);

        // 2. The industry, plan and workspace code must be valid.
        var key = i.SignInKey.Trim().ToLowerInvariant();
        var email = i.Email.Trim();
        if (onboarding.IndustryViews().All(v => v.Key != i.Industry)) throw new FieldValidationException("industry", "Choose an industry from the list.");
        var planId = i.PlanId ?? settings.DefaultPlanId;
        var plan = planId is null ? null : await plans.FindPublicAsync(planId.Value, ct);
        if (plan is null) throw new FieldValidationException("planId", "Choose a plan.");
        if (await workflow.KeyTakenAsync(key, email, ct)) throw new FieldValidationException("signInKey", "That code is taken. Try another.");

        var hash = await directory.PrepareSignupPasswordAsync(email, i.Password, ct);

        // 3. Registering again replaces an earlier one that is still waiting (a lost email, a typo).
        var earlier = await signups.ListPendingForEmailForUpdateAsync(email, ct);
        foreach (var e in earlier) { e.Status = SignupStatus.Expired; e.PasswordHash = null; e.VerificationTokenHash = null; }

        // 4. Keep the registration, waiting for email confirmation or review.
        var request = new SignupRequest
        {
            BusinessName = i.BusinessName.Trim(), SignInKey = key, Industry = i.Industry, CountryCode = i.CountryCode.ToUpperInvariant(), BaseCurrency = i.BaseCurrency.ToUpperInvariant(),
            TimeZone = i.TimeZone, ContactName = i.ContactName.Trim(), Email = email, Phone = i.Phone?.Trim(), PasswordHash = hash, PlanId = plan.Id, Cycle = i.Cycle ?? BillingCycle.Monthly, CreatedIp = command.Ip, TermsAcceptedAt = clock.UtcNow,
        };
        signups.Add(request);

        string? token = null;
        if (workflow.Verifies(settings))
        {
            token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            request.Status = SignupStatus.PendingEmail;
            request.VerificationTokenHash = SignupWorkflow.HashToken(token);
            request.VerificationExpiresAt = clock.UtcNow.AddHours(SignupWorkflow.VerificationHours);
        }
        else
        {
            // Waits for a person, or is provisioned just below. Asked to verify with no email server to do it: a person
            // checks instead of letting it through unverified.
            request.Status = SignupStatus.PendingReview;
        }
        log.Add("Registration received", null, request.BusinessName, $"{email}, {plan.Name} plan");
        await signups.SaveChangesAsync(ct);

        // 5. Ask for the email confirmation, provision at once, or tell the founder.
        if (token is not null)
        {
            await mailer.SendAsync(email, "Confirm your email",
                $"Please confirm your email to continue setting up {request.BusinessName}:\n{mailer.Link($"/register/verify?token={Uri.EscapeDataString(token)}")}\n\nThis link works for {SignupWorkflow.VerificationHours} hours. If this was not you, ignore this email.", ct);
            return new(SignupStatus.PendingEmail, $"We sent a link to {email}. Open it to confirm your email.", null);
        }
        if (settings.SignupMode == SignupMode.Open && !settings.RequireEmailVerification) return await workflow.ProvisionAsync(request, ct);

        await workflow.NotifyFounderAsync(settings, request, ct);
        return new(SignupStatus.PendingReview, SignupWorkflow.ReviewMessage, null);
    }
}
