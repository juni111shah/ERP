using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class VerifySignupHandler(ISignupRequestRepository signups, PlatformSettingsStore settingsStore, SignupWorkflow workflow, IClock clock)
    : ICommandHandler<VerifySignupCommand, SignupResult>
{
    public async Task<SignupResult> Handle(VerifySignupCommand command, CancellationToken ct)
    {
        // 1. The link must match a registration still waiting for its email.
        var hash = SignupWorkflow.HashToken(command.Token ?? "");
        var request = await signups.FindPendingEmailByTokenForUpdateAsync(hash, ct)
            ?? throw new ConflictException("This link has already been used or is no longer valid.");
        if (request.VerificationExpiresAt < clock.UtcNow)
        {
            request.Status = SignupStatus.Expired;
            request.PasswordHash = null;
            request.VerificationTokenHash = null;
            await signups.SaveChangesAsync(ct);
            throw new ConflictException("This link has expired. Please register again.");
        }

        // 2. Mark the email confirmed.
        var settings = await settingsStore.GetAsync(ct);
        request.EmailVerifiedAt = clock.UtcNow;
        request.VerificationTokenHash = null;
        request.Status = SignupStatus.PendingReview;
        await signups.SaveChangesAsync(ct);

        // 3. Provision at once when registration is open; otherwise a person reviews it.
        if (settings.SignupMode == SignupMode.Open) return await workflow.ProvisionAsync(request, ct);
        await workflow.NotifyFounderAsync(settings, request, ct);
        return new(SignupStatus.PendingReview, SignupWorkflow.ReviewMessage, null);
    }
}
