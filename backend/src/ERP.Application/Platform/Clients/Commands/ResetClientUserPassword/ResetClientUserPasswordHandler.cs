using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class ResetClientUserPasswordHandler(
    IReadRepository<Organization> organizations, IRepository<WorkspaceSubscription> subscriptions, IPlatformDirectory directory, ERP.Application.Auth.IAuthService auth, PlatformLog log)
    : ICommandHandler<ResetClientUserPasswordCommand, TemporaryPassword>
{
    public async Task<TemporaryPassword> Handle(ResetClientUserPasswordCommand command, CancellationToken ct)
    {
        var org = await organizations.GetAsync(command.OrgId, ct);
        var member = (await directory.MembersAsync(command.OrgId, ct)).FirstOrDefault(m => m.UserId == command.UserId) ?? throw new NotFoundException();
        var password = await directory.IssueTemporaryPasswordAsync(command.OrgId, command.UserId, ct);
        await auth.RevokeSessionsAsync(command.UserId, null, "password-reset-by-platform", ct);
        log.Add("Temporary password issued", command.OrgId, org.Name, member.Email);
        await subscriptions.SaveChangesAsync(ct);
        return new(password);
    }
}
