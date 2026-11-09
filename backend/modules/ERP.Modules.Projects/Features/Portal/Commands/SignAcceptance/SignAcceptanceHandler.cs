using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SignAcceptanceHandler(
    IProjectAcceptanceRepository acceptances,
    IProjectMilestoneRepository milestones,
    IProjectRepository projects,
    ICurrentUser user)
    : ICommandHandler<SignAcceptanceCommand, PortalAcceptanceDto>
{
    public async Task<PortalAcceptanceDto> Handle(SignAcceptanceCommand command, CancellationToken ct)
    {
        // 1. Only the customer's own sign-off, and only once.
        var me = PortalCustomer.IdOf(user);
        var a = await acceptances.GetForUpdateAsync(command.Id, ct);
        var p = await projects.GetAsync(a.ProjectId, ct);
        if (p.CustomerId != me) throw new NotFoundException();   // someone else's project looks like it does not exist
        if (a.Status != AcceptanceStatus.Pending) throw new ConflictException("This sign-off has already been answered.");

        // 2. Record the answer with its evidence.
        var i = command.Input;
        a.Status = i.Accept ? AcceptanceStatus.Signed : AcceptanceStatus.Declined;
        a.SignedByName = i.SignedByName.Trim(); a.Signature = i.Accept ? i.Signature?.Trim() : null; a.Comment = i.Comment?.Trim(); a.SignedAt = ERP.Domain.Common.AppClock.UtcNow;
        a.SignatureImage = i.Accept ? i.SignatureImage : null;
        a.SignedIp = command.ClientIp;
        a.EvidenceHash = Evidence.Hash(a);

        // 3. Signing a milestone's sign-off marks the milestone as achieved; save.
        if (i.Accept && a.MilestoneId is { } mid)
        {
            var m = await milestones.GetForUpdateAsync(mid, ct);
            if (m.Status == MilestoneStatus.Pending) { m.Status = MilestoneStatus.Achieved; m.AchievedDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow); await milestones.SaveChangesAsync(ct); }
        }
        await acceptances.SaveChangesAsync(ct);
        return PortalAcceptanceDto.From(a, p.Name);
    }
}
