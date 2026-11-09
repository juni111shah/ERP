using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class ApproveSignupHandler(ISignupRequestRepository signups, IReadRepository<SubscriptionPlan> plans, ClientProvisioner provisioner, PlatformLog log, IClock clock, ICurrentUser user)
    : ICommandHandler<ApproveSignupCommand, SignupRow>
{
    public async Task<SignupRow> Handle(ApproveSignupCommand command, CancellationToken ct)
    {
        var r = await signups.GetForUpdateAsync(command.Id, ct);
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
        return SignupRow.From(r, (await plans.GetAsync(r.PlanId, ct)).Name);
    }
}
