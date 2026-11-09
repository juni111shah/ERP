using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class RequestAcceptanceHandler(
    IProjectAcceptanceRepository acceptances,
    IProjectMilestoneRepository milestones,
    IProjectRepository projects,
    ProjectPeople people,
    ProjectAccess access,
    ICurrentUser user)
    : ICommandHandler<RequestAcceptanceCommand, AcceptanceDto>
{
    public async Task<AcceptanceDto> Handle(RequestAcceptanceCommand command, CancellationToken ct)
    {
        // 1. An open project with a customer; the milestone must be its own and nothing may be waiting already.
        var i = command.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);
        if (p.CustomerId is null) throw new ConflictException("The project has no customer to sign it off.");
        if (i.MilestoneId is { } mid && !await milestones.AnyAsync(m => m.Id == mid && m.ProjectId == p.Id, ct)) throw new FieldValidationException("milestoneId", "Milestone not found in this project.");
        if (await acceptances.AnyAsync(a => a.ProjectId == p.Id && a.MilestoneId == i.MilestoneId && a.Status == AcceptanceStatus.Pending, ct)) throw new ConflictException("A sign-off is already waiting for the customer.");

        // 2. Ask and save.
        var a = new ProjectAcceptance { ProjectId = p.Id, MilestoneId = i.MilestoneId, Title = i.Title.Trim(), RequestedAt = ERP.Domain.Common.AppClock.UtcNow, RequestedByName = await people.NameAsync(user.UserId, ct) };
        acceptances.Add(a);
        await acceptances.SaveChangesAsync(ct);
        return new AcceptanceDto(a.Id, p.Id, p.Name, a.MilestoneId, a.Title, a.Status, a.RequestedAt, null, null, null);
    }
}
