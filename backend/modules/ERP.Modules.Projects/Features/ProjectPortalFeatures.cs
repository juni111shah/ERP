using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

// ---- staff side: ask the customer to sign off ---------------------------------------------------------------------------------
public sealed record RequestAcceptanceInput(Guid ProjectId, Guid? MilestoneId, string Title);
public sealed record AcceptanceDto(Guid Id, Guid ProjectId, string ProjectName, Guid? MilestoneId, string Title, AcceptanceStatus Status, DateTime RequestedAt, string? SignedByName, DateTime? SignedAt, string? Comment);

public sealed record RequestAcceptance(RequestAcceptanceInput Input) : ICommand<AcceptanceDto>, IRequirePermission { public string RequiredPermission => P.MilestonesManage; }
public sealed record ListAcceptances(Guid? ProjectId) : IQuery<IReadOnlyList<AcceptanceDto>>, IRequirePermission { public string RequiredPermission => P.MilestonesRead; }

public sealed class RequestAcceptanceValidator : AbstractValidator<RequestAcceptance>
{
    public RequestAcceptanceValidator() { RuleFor(x => x.Input.ProjectId).NotEmpty(); RuleFor(x => x.Input.Title).NotEmpty().MaximumLength(200); }
}

// ---- customer portal ----------------------------------------------------------------------------------------------------------
public sealed record PortalMilestoneDto(Guid Id, string Title, DateOnly DueDate, MilestoneStatus Status, DateOnly? AchievedDate);
public sealed record PortalProjectDto(Guid Id, string Number, string Name, ProjectStatus Status, DateOnly? StartDate, DateOnly? EndDate, decimal PercentComplete, string? ManagerName, IReadOnlyList<PortalMilestoneDto> Milestones);
public sealed record PortalAcceptanceDto(Guid Id, Guid ProjectId, string ProjectName, string Title, AcceptanceStatus Status, DateTime RequestedAt, string? SignedByName, DateTime? SignedAt);
public sealed record SignAcceptanceInput(bool Accept, string SignedByName, string? Signature, string? Comment);

public sealed record GetPortalProjects : IQuery<IReadOnlyList<PortalProjectDto>>;
public sealed record ListPortalAcceptances : IQuery<IReadOnlyList<PortalAcceptanceDto>>;
/// <summary>The customer signs (types their name) or declines. Signing a milestone's sign-off marks the milestone as achieved.</summary>
public sealed record SignAcceptance(Guid Id, SignAcceptanceInput Input) : ICommand<PortalAcceptanceDto>;

public sealed class SignAcceptanceValidator : AbstractValidator<SignAcceptance>
{
    public SignAcceptanceValidator()
    {
        RuleFor(x => x.Input.SignedByName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Input.Signature).MaximumLength(150);
        RuleFor(x => x.Input.Comment).MaximumLength(1000);
        RuleFor(x => x.Input.Comment).NotEmpty().When(x => !x.Input.Accept).WithMessage("Say why you decline.");
        RuleFor(x => x.Input.Signature).NotEmpty().When(x => x.Input.Accept).WithMessage("Type your name as the signature.");
    }
}

internal sealed class AcceptanceHandlers(
    IRepository<ProjectAcceptance> acceptances,
    IRepository<ProjectMilestone> milestones,
    IReadRepository<Project> projects,
    IReadRepository<ProjectTask> tasks,
    ICurrentUser user,
    ProjectPeople people) :
    IRequestHandler<RequestAcceptance, AcceptanceDto>,
    IRequestHandler<ListAcceptances, IReadOnlyList<AcceptanceDto>>,
    IRequestHandler<GetPortalProjects, IReadOnlyList<PortalProjectDto>>,
    IRequestHandler<ListPortalAcceptances, IReadOnlyList<PortalAcceptanceDto>>,
    IRequestHandler<SignAcceptance, PortalAcceptanceDto>
{
    private Guid Customer => user.PortalKind == "customer" && user.PortalPartyId is { } id ? id : throw new ForbiddenException();

    public async Task<AcceptanceDto> Handle(RequestAcceptance cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        if (p.CustomerId is null) throw new ConflictException("The project has no customer to sign it off.");
        if (i.MilestoneId is { } mid && !await milestones.AnyAsync(m => m.Id == mid && m.ProjectId == p.Id, ct)) throw new FieldValidationException("milestoneId", "Milestone not found in this project.");
        if (await acceptances.AnyAsync(a => a.ProjectId == p.Id && a.MilestoneId == i.MilestoneId && a.Status == AcceptanceStatus.Pending, ct)) throw new ConflictException("A sign-off is already waiting for the customer.");
        var a = new ProjectAcceptance { ProjectId = p.Id, MilestoneId = i.MilestoneId, Title = i.Title.Trim(), RequestedAt = DateTime.UtcNow, RequestedByName = await people.NameAsync(user.UserId, ct) };
        acceptances.Add(a);
        await acceptances.SaveChangesAsync(ct);
        return new AcceptanceDto(a.Id, p.Id, p.Name, a.MilestoneId, a.Title, a.Status, a.RequestedAt, null, null, null);
    }

    public async Task<IReadOnlyList<AcceptanceDto>> Handle(ListAcceptances q, CancellationToken ct)
    {
        var rows = await acceptances.Query(new Specification<ProjectAcceptance>(a => q.ProjectId == null || a.ProjectId == q.ProjectId)).ToListAsync(ct);
        var ids = rows.Select(a => a.ProjectId).Distinct().ToList();
        var names = await projects.Query(new Specification<Project>(p => ids.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return rows.OrderByDescending(a => a.RequestedAt).Select(a => new AcceptanceDto(a.Id, a.ProjectId, names.GetValueOrDefault(a.ProjectId, ""), a.MilestoneId, a.Title, a.Status, a.RequestedAt, a.SignedByName, a.SignedAt, a.Comment)).ToList();
    }

    public async Task<IReadOnlyList<PortalProjectDto>> Handle(GetPortalProjects q, CancellationToken ct)
    {
        var me = Customer;
        var mine = await projects.Query(new Specification<Project>(p => p.CustomerId == me && !p.IsTemplate && !p.IsArchived && p.Status != ProjectStatus.Planning && p.Status != ProjectStatus.Cancelled)).ToListAsync(ct);
        var ids = mine.Select(p => p.Id).ToList();
        var ms = (await milestones.Query(new Specification<ProjectMilestone>(m => ids.Contains(m.ProjectId) && m.Status != MilestoneStatus.Cancelled)).ToListAsync(ct)).ToLookup(m => m.ProjectId);
        var done = await TaskProgressAsync(ids, ct);
        return mine.OrderBy(p => p.Number).Select(p => new PortalProjectDto(p.Id, p.Number, p.Name, p.Status, p.StartDate, p.EndDate, done.GetValueOrDefault(p.Id), p.ManagerName,
            ms[p.Id].OrderBy(m => m.DueDate).Select(m => new PortalMilestoneDto(m.Id, m.Title, m.DueDate, m.Status, m.AchievedDate)).ToList())).ToList();
    }

    private async Task<Dictionary<Guid, decimal>> TaskProgressAsync(List<Guid> projectIds, CancellationToken ct)
    {
        var rows = await tasks.Query(new Specification<ProjectTask>(t => projectIds.Contains(t.ProjectId))).ToListAsync(ct);
        return rows.GroupBy(t => t.ProjectId).ToDictionary(g => g.Key, g => ProjectMath.PercentComplete(g));
    }

    public async Task<IReadOnlyList<PortalAcceptanceDto>> Handle(ListPortalAcceptances q, CancellationToken ct)
    {
        var me = Customer;
        var mineIds = await projects.Query(new Specification<Project>(p => p.CustomerId == me)).Select(p => p.Id).ToListAsync(ct);
        var names = await projects.Query(new Specification<Project>(p => mineIds.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var rows = await acceptances.Query(new Specification<ProjectAcceptance>(a => mineIds.Contains(a.ProjectId))).ToListAsync(ct);
        return rows.OrderBy(a => a.Status).ThenByDescending(a => a.RequestedAt).Select(a => Portal(a, names.GetValueOrDefault(a.ProjectId, ""))).ToList();
    }

    public async Task<PortalAcceptanceDto> Handle(SignAcceptance cmd, CancellationToken ct)
    {
        var me = Customer;
        var a = await acceptances.GetForUpdateAsync(cmd.Id, ct);
        var p = await projects.GetAsync(a.ProjectId, ct);
        if (p.CustomerId != me) throw new NotFoundException();   // someone else's project looks like it does not exist
        if (a.Status != AcceptanceStatus.Pending) throw new ConflictException("This sign-off has already been answered.");
        var i = cmd.Input;
        a.Status = i.Accept ? AcceptanceStatus.Signed : AcceptanceStatus.Declined;
        a.SignedByName = i.SignedByName.Trim(); a.Signature = i.Accept ? i.Signature?.Trim() : null; a.Comment = i.Comment?.Trim(); a.SignedAt = DateTime.UtcNow;
        if (i.Accept && a.MilestoneId is { } mid)
        {
            var m = await milestones.GetForUpdateAsync(mid, ct);
            if (m.Status == MilestoneStatus.Pending) { m.Status = MilestoneStatus.Achieved; m.AchievedDate = DateOnly.FromDateTime(DateTime.UtcNow); await milestones.SaveChangesAsync(ct); }
        }
        await acceptances.SaveChangesAsync(ct);
        return Portal(a, p.Name);
    }

    private static PortalAcceptanceDto Portal(ProjectAcceptance a, string project) => new(a.Id, a.ProjectId, project, a.Title, a.Status, a.RequestedAt, a.SignedByName, a.SignedAt);
}
