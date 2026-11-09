using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class RejectSignupHandler(ISignupRequestRepository signups, IReadRepository<SubscriptionPlan> plans, PlatformLog log, IPlatformMailer mailer, IClock clock, ICurrentUser user)
    : ICommandHandler<RejectSignupCommand, SignupRow>
{
    public async Task<SignupRow> Handle(RejectSignupCommand command, CancellationToken ct)
    {
        var r = await signups.GetForUpdateAsync(command.Id, ct);
        if (r.Status is not (SignupStatus.PendingReview or SignupStatus.PendingEmail)) throw new ConflictException("This registration has already been handled.");
        r.Status = SignupStatus.Rejected;
        r.RejectReason = command.Reason?.Trim();
        r.PasswordHash = null;
        r.VerificationTokenHash = null;
        r.ReviewedAt = clock.UtcNow;
        r.ReviewedBy = user.UserId;
        log.Add("Registration rejected", null, r.BusinessName, $"{r.Email}. {r.RejectReason}");
        await signups.SaveChangesAsync(ct);
        await mailer.SendAsync(r.Email, "About your registration", $"We could not set up {r.BusinessName} at this time.{(r.RejectReason is { Length: > 0 } reason ? $"\n\n{reason}" : "")}", ct);
        return SignupRow.From(r, (await plans.GetAsync(r.PlanId, ct)).Name);
    }
}
