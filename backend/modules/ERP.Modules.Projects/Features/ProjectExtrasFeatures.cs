using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

// ---- rate cards ---------------------------------------------------------------------------------------------------------------
public sealed record RateInput(Guid? ProjectId, Guid? UserId, string? Role, decimal BillRate, decimal CostRate);
public sealed record RateDto(Guid Id, Guid? ProjectId, Guid? UserId, string? UserName, string? Role, decimal BillRate, decimal CostRate);
public sealed record ListRates(Guid? ProjectId) : IQuery<IReadOnlyList<RateDto>>, IRequirePermission { public string RequiredPermission => P.RatesRead; }
public sealed record SaveRate(Guid? Id, RateInput Input) : ICommand<RateDto>, IRequirePermission { public string RequiredPermission => P.RatesManage; }
public sealed record DeleteRate(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.RatesManage; }

public sealed class SaveRateValidator : AbstractValidator<SaveRate>
{
    public SaveRateValidator()
    {
        RuleFor(x => x.Input.BillRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.CostRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.Role).MaximumLength(100);
        RuleFor(x => x.Input).Must(i => i.UserId is null || string.IsNullOrWhiteSpace(i.Role)).WithName("role").WithMessage("A rate is for a person or for a role, not both.");
    }
}

// ---- comments -----------------------------------------------------------------------------------------------------------------
public sealed record CommentInput(string OwnerType, Guid OwnerId, string Text);
public sealed record CommentDto(Guid Id, string OwnerType, Guid OwnerId, Guid? UserId, string AuthorName, string Text, DateTime CreatedAt);
public sealed record ListComments(string OwnerType, Guid OwnerId) : IQuery<IReadOnlyList<CommentDto>>, IRequirePermission { public string RequiredPermission => P.ProjectsRead; }
public sealed record AddComment(CommentInput Input) : ICommand<CommentDto>, IRequirePermission { public string RequiredPermission => P.ProjectsRead; }
public sealed record DeleteComment(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ProjectsRead; }

public sealed class AddCommentValidator : AbstractValidator<AddComment>
{
    public AddCommentValidator()
    {
        RuleFor(x => x.Input.OwnerType).Must(t => t is "project" or "task").WithMessage("Comments go on a project or a task.");
        RuleFor(x => x.Input.OwnerId).NotEmpty();
        RuleFor(x => x.Input.Text).NotEmpty().MaximumLength(2000);
    }
}

// ---- change orders ------------------------------------------------------------------------------------------------------------
public sealed record ChangeOrderInput(Guid ProjectId, string Title, string? Description, decimal AmountChange, decimal HoursChange = 0m);
public sealed record ChangeOrderDto(Guid Id, Guid ProjectId, string Number, string Title, string? Description, decimal AmountChange, decimal HoursChange, ChangeOrderStatus Status, DateTime? DecidedAt, string? DecisionNote, DateTime CreatedAt);
public sealed record ListChangeOrders(Guid? ProjectId) : IQuery<IReadOnlyList<ChangeOrderDto>>, IRequirePermission { public string RequiredPermission => P.ChangeOrdersRead; }
public sealed record CreateChangeOrder(ChangeOrderInput Input) : ICommand<ChangeOrderDto>, IRequirePermission { public string RequiredPermission => P.ChangeOrdersManage; }
public sealed record UpdateChangeOrder(Guid Id, ChangeOrderInput Input) : ICommand<ChangeOrderDto>, IRequirePermission { public string RequiredPermission => P.ChangeOrdersManage; }
public sealed record DeleteChangeOrder(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ChangeOrdersManage; }
public sealed record DecideChangeOrder(Guid Id, bool Approve, string? Note) : ICommand<ChangeOrderDto>, IRequirePermission { public string RequiredPermission => P.ChangeOrdersManage; }

public sealed class ChangeOrderInputValidator : AbstractValidator<ChangeOrderInput>
{
    public ChangeOrderInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AmountChange).InclusiveBetween(-1_000_000_000m, 1_000_000_000m);
    }
}
public sealed class CreateChangeOrderValidator : AbstractValidator<CreateChangeOrder> { public CreateChangeOrderValidator() => RuleFor(x => x.Input).SetValidator(new ChangeOrderInputValidator()); }
public sealed class UpdateChangeOrderValidator : AbstractValidator<UpdateChangeOrder> { public UpdateChangeOrderValidator() => RuleFor(x => x.Input).SetValidator(new ChangeOrderInputValidator()); }

// ---- risks and issues ---------------------------------------------------------------------------------------------------------
public sealed record RiskInput(Guid ProjectId, RiskKind Kind, string Title, string? Notes, RiskSeverity Severity, RiskStatus Status, Guid? OwnerUserId = null, string? OwnerName = null, DateOnly? DueDate = null);
public sealed record RiskDto(Guid Id, Guid ProjectId, RiskKind Kind, string Title, string? Notes, RiskSeverity Severity, RiskStatus Status, Guid? OwnerUserId, string? OwnerName, DateOnly? DueDate, DateTime CreatedAt);
public sealed record ListRisks(Guid? ProjectId) : IQuery<IReadOnlyList<RiskDto>>, IRequirePermission { public string RequiredPermission => P.RisksRead; }
public sealed record SaveRisk(Guid? Id, RiskInput Input) : ICommand<RiskDto>, IRequirePermission { public string RequiredPermission => P.RisksManage; }
public sealed record DeleteRisk(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.RisksManage; }

public sealed class SaveRiskValidator : AbstractValidator<SaveRisk>
{
    public SaveRiskValidator()
    {
        RuleFor(x => x.Input.ProjectId).NotEmpty();
        RuleFor(x => x.Input.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Input.Notes).MaximumLength(2000);
        RuleFor(x => x.Input.OwnerName).MaximumLength(150);
        RuleFor(x => x.Input.Kind).IsInEnum();
        RuleFor(x => x.Input.Severity).IsInEnum();
        RuleFor(x => x.Input.Status).IsInEnum();
    }
}

// ---- team and allocation ------------------------------------------------------------------------------------------------------
public sealed record MemberDto(Guid Id, Guid ProjectId, Guid UserId, string Name, string? Role);
public sealed record ListMembers(Guid ProjectId) : IQuery<IReadOnlyList<MemberDto>>, IRequirePermission { public string RequiredPermission => P.ProjectsRead; }
public sealed record AddMember(Guid ProjectId, Guid UserId, string? Role) : ICommand<MemberDto>, IRequirePermission { public string RequiredPermission => P.TeamManage; }
public sealed record RemoveMember(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.TeamManage; }
public sealed record AllocationInput(Guid ProjectId, Guid UserId, DateOnly FromDate, DateOnly ToDate, decimal HoursPerWeek);
public sealed record AllocationDto(Guid Id, Guid ProjectId, Guid UserId, string UserName, DateOnly FromDate, DateOnly ToDate, decimal HoursPerWeek);
public sealed record ListAllocations(Guid? ProjectId, Guid? UserId) : IQuery<IReadOnlyList<AllocationDto>>, IRequirePermission { public string RequiredPermission => P.ProjectsRead; }
public sealed record SaveAllocation(Guid? Id, AllocationInput Input) : ICommand<AllocationDto>, IRequirePermission { public string RequiredPermission => P.TeamManage; }
public sealed record DeleteAllocation(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.TeamManage; }

public sealed class AddMemberValidator : AbstractValidator<AddMember> { public AddMemberValidator() { RuleFor(x => x.ProjectId).NotEmpty(); RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.Role).MaximumLength(100); } }
public sealed class SaveAllocationValidator : AbstractValidator<SaveAllocation>
{
    public SaveAllocationValidator()
    {
        RuleFor(x => x.Input.ProjectId).NotEmpty();
        RuleFor(x => x.Input.UserId).NotEmpty();
        RuleFor(x => x.Input.HoursPerWeek).GreaterThan(0).LessThanOrEqualTo(168);
        RuleFor(x => x.Input).Must(i => i.ToDate >= i.FromDate).WithName("toDate").WithMessage("The end date cannot be before the start date.");
    }
}

internal sealed class ExtrasHandlers(
    IRepository<ProjectRate> rates,
    IRepository<ProjectComment> comments,
    IRepository<ProjectChangeOrder> changeOrders,
    IRepository<ProjectRisk> risks,
    IRepository<ProjectMember> members,
    IRepository<ProjectAllocation> allocations,
    IReadRepository<Project> projects,
    IReadRepository<ProjectTask> tasks,
    ProjectPeople people,
    ProjectAccess access,
    ICurrentUser user) :
    IRequestHandler<ListRates, IReadOnlyList<RateDto>>,
    IRequestHandler<SaveRate, RateDto>,
    IRequestHandler<DeleteRate, Unit>,
    IRequestHandler<ListComments, IReadOnlyList<CommentDto>>,
    IRequestHandler<AddComment, CommentDto>,
    IRequestHandler<DeleteComment, Unit>,
    IRequestHandler<ListChangeOrders, IReadOnlyList<ChangeOrderDto>>,
    IRequestHandler<CreateChangeOrder, ChangeOrderDto>,
    IRequestHandler<UpdateChangeOrder, ChangeOrderDto>,
    IRequestHandler<DeleteChangeOrder, Unit>,
    IRequestHandler<DecideChangeOrder, ChangeOrderDto>,
    IRequestHandler<ListRisks, IReadOnlyList<RiskDto>>,
    IRequestHandler<SaveRisk, RiskDto>,
    IRequestHandler<DeleteRisk, Unit>,
    IRequestHandler<ListMembers, IReadOnlyList<MemberDto>>,
    IRequestHandler<AddMember, MemberDto>,
    IRequestHandler<RemoveMember, Unit>,
    IRequestHandler<ListAllocations, IReadOnlyList<AllocationDto>>,
    IRequestHandler<SaveAllocation, AllocationDto>,
    IRequestHandler<DeleteAllocation, Unit>
{
    // ---- rates
    public async Task<IReadOnlyList<RateDto>> Handle(ListRates q, CancellationToken ct)
    {
        var rows = await rates.Query(new Specification<ProjectRate>(r => q.ProjectId == null || r.ProjectId == q.ProjectId || r.ProjectId == null)).ToListAsync(ct);
        var names = await people.NamesAsync(rows.Where(r => r.UserId != null).Select(r => r.UserId!.Value), ct);
        return rows.OrderBy(r => r.ProjectId == null ? 1 : 0).ThenBy(r => r.Role).Select(r => ToDto(r, r.UserId is { } u ? names.GetValueOrDefault(u) : null)).ToList();
    }

    public async Task<RateDto> Handle(SaveRate cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (i.ProjectId is { } pid) _ = await projects.GetAsync(pid, ct);
        string? userName = null;
        if (i.UserId is { } uid) userName = await people.RequireNameAsync(uid, "userId", ct);
        var role = string.IsNullOrWhiteSpace(i.Role) ? null : i.Role.Trim();
        var all = await rates.Query(new Specification<ProjectRate>(r => r.ProjectId == i.ProjectId && r.UserId == i.UserId)).ToListAsync(ct);
        if (all.Any(r => r.Id != cmd.Id && string.Equals(r.Role, role, StringComparison.OrdinalIgnoreCase))) throw new ConflictException("There is already a rate for that project, person and role.");
        ProjectRate row;
        if (cmd.Id is { } id) row = await rates.GetForUpdateAsync(id, ct); else { row = new ProjectRate(); rates.Add(row); }
        row.ProjectId = i.ProjectId; row.UserId = i.UserId; row.Role = role; row.BillRate = i.BillRate; row.CostRate = i.CostRate;
        await rates.SaveChangesAsync(ct);
        return ToDto(row, userName);
    }

    public async Task<Unit> Handle(DeleteRate cmd, CancellationToken ct)
    {
        rates.Remove(await rates.GetForUpdateAsync(cmd.Id, ct));
        await rates.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private static RateDto ToDto(ProjectRate r, string? userName) => new(r.Id, r.ProjectId, r.UserId, userName, r.Role, r.BillRate, r.CostRate);

    // ---- comments
    public async Task<IReadOnlyList<CommentDto>> Handle(ListComments q, CancellationToken ct)
    {
        await access.EnsureProjectAsync(q.OwnerType == "task" ? (await tasks.GetAsync(q.OwnerId, ct)).ProjectId : q.OwnerId, ct);
        var rows = await comments.Query(new Specification<ProjectComment>(c => c.OwnerType == q.OwnerType && c.OwnerId == q.OwnerId)).ToListAsync(ct);
        return rows.OrderBy(c => c.CreatedAt).Select(CommentOf).ToList();
    }

    public async Task<CommentDto> Handle(AddComment cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        Guid projectId;
        if (i.OwnerType == "project") { projectId = (await projects.GetAsync(i.OwnerId, ct)).Id; }
        else projectId = (await tasks.GetAsync(i.OwnerId, ct)).ProjectId;
        await access.EnsureProjectAsync(projectId, ct);
        var author = await people.NameAsync(user.UserId, ct) ?? user.Email ?? "Someone";
        var c = new ProjectComment { OwnerType = i.OwnerType, OwnerId = i.OwnerId, ProjectId = projectId, UserId = user.UserId, AuthorName = author, Text = i.Text.Trim() };
        comments.Add(c);
        await comments.SaveChangesAsync(ct);
        return CommentOf(c);
    }

    public async Task<Unit> Handle(DeleteComment cmd, CancellationToken ct)
    {
        var c = await comments.GetForUpdateAsync(cmd.Id, ct);
        if (c.UserId != user.UserId && !access.Has(P.ProjectsManage)) throw new ForbiddenException("Only the author or a project manager can delete a comment.");
        comments.Remove(c);
        await comments.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private static CommentDto CommentOf(ProjectComment c) => new(c.Id, c.OwnerType, c.OwnerId, c.UserId, c.AuthorName, c.Text, c.CreatedAt);

    // ---- change orders
    public async Task<IReadOnlyList<ChangeOrderDto>> Handle(ListChangeOrders q, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        var rows = await changeOrders.Query(new Specification<ProjectChangeOrder>(c => q.ProjectId == null || c.ProjectId == q.ProjectId)).ToListAsync(ct);
        return rows.Where(c => visible == null || visible.Contains(c.ProjectId)).OrderByDescending(c => c.CreatedAt).Select(ChangeOf).ToList();
    }

    public async Task<ChangeOrderDto> Handle(CreateChangeOrder cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);
        // numbered after the highest one so far: a deleted draft must not make the next number repeat
        var used = await changeOrders.Query(new Specification<ProjectChangeOrder>(c => c.ProjectId == p.Id)).Select(c => c.Number).ToListAsync(ct);
        var next = used.Select(n => int.TryParse(n[(n.LastIndexOf('-') + 1)..], out var v) ? v : 0).DefaultIfEmpty(0).Max() + 1;
        var c = new ProjectChangeOrder { ProjectId = p.Id, Number = $"CO-{p.Number}-{next:00}" };
        Fill(c, i);
        changeOrders.Add(c);
        await changeOrders.SaveChangesAsync(ct);
        return ChangeOf(c);
    }

    public async Task<ChangeOrderDto> Handle(UpdateChangeOrder cmd, CancellationToken ct)
    {
        var c = await changeOrders.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureProjectAsync(c.ProjectId, ct);
        if (c.Status != ChangeOrderStatus.Draft) throw new ConflictException("Only a draft change order can be edited.");
        Fill(c, cmd.Input);
        await changeOrders.SaveChangesAsync(ct);
        return ChangeOf(c);
    }

    public async Task<Unit> Handle(DeleteChangeOrder cmd, CancellationToken ct)
    {
        var c = await changeOrders.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureProjectAsync(c.ProjectId, ct);
        if (c.Status != ChangeOrderStatus.Draft) throw new ConflictException("Only a draft change order can be deleted.");
        changeOrders.Remove(c);
        await changeOrders.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<ChangeOrderDto> Handle(DecideChangeOrder cmd, CancellationToken ct)
    {
        var c = await changeOrders.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureProjectAsync(c.ProjectId, ct);
        if (c.Status != ChangeOrderStatus.Draft) throw new ConflictException("This change order has already been decided.");
        ProjectRules.EnsureOpen(await projects.GetAsync(c.ProjectId, ct));
        c.Status = cmd.Approve ? ChangeOrderStatus.Approved : ChangeOrderStatus.Rejected;
        c.DecidedAt = ERP.Domain.Common.AppClock.UtcNow; c.DecisionNote = cmd.Note?.Trim();
        await changeOrders.SaveChangesAsync(ct);
        return ChangeOf(c);
    }

    private static void Fill(ProjectChangeOrder c, ChangeOrderInput i) { c.Title = i.Title.Trim(); c.Description = i.Description?.Trim(); c.AmountChange = i.AmountChange; c.HoursChange = i.HoursChange; }
    private static ChangeOrderDto ChangeOf(ProjectChangeOrder c) => new(c.Id, c.ProjectId, c.Number, c.Title, c.Description, c.AmountChange, c.HoursChange, c.Status, c.DecidedAt, c.DecisionNote, c.CreatedAt);

    // ---- risks
    public async Task<IReadOnlyList<RiskDto>> Handle(ListRisks q, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        var rows = (await risks.Query(new Specification<ProjectRisk>(r => q.ProjectId == null || r.ProjectId == q.ProjectId)).ToListAsync(ct)).Where(r => visible == null || visible.Contains(r.ProjectId)).ToList();
        return rows.OrderBy(r => r.Status).ThenByDescending(r => r.Severity).ThenBy(r => r.DueDate).Select(RiskOf).ToList();
    }

    public async Task<RiskDto> Handle(SaveRisk cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        await access.EnsureAsync(p, ct);
        string? owner = i.OwnerName?.Trim();
        if (i.OwnerUserId is { } uid) owner = await people.RequireNameAsync(uid, "ownerUserId", ct);
        ProjectRisk r;
        if (cmd.Id is { } id) { r = await risks.GetForUpdateAsync(id, ct); if (r.ProjectId != p.Id) throw new FieldValidationException("projectId", "A risk cannot move to another project."); }
        else { r = new ProjectRisk { ProjectId = p.Id }; risks.Add(r); }
        r.Kind = i.Kind; r.Title = i.Title.Trim(); r.Notes = i.Notes?.Trim(); r.Severity = i.Severity; r.Status = i.Status; r.OwnerUserId = i.OwnerUserId; r.OwnerName = owner; r.DueDate = i.DueDate;
        await risks.SaveChangesAsync(ct);
        return RiskOf(r);
    }

    public async Task<Unit> Handle(DeleteRisk cmd, CancellationToken ct)
    {
        var risk = await risks.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureProjectAsync(risk.ProjectId, ct);
        risks.Remove(risk);
        await risks.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private static RiskDto RiskOf(ProjectRisk r) => new(r.Id, r.ProjectId, r.Kind, r.Title, r.Notes, r.Severity, r.Status, r.OwnerUserId, r.OwnerName, r.DueDate, r.CreatedAt);

    // ---- team
    public async Task<IReadOnlyList<MemberDto>> Handle(ListMembers q, CancellationToken ct)
    {
        await access.EnsureProjectAsync(q.ProjectId, ct);
        return (await members.Query(new Specification<ProjectMember>(m => m.ProjectId == q.ProjectId)).ToListAsync(ct)).OrderBy(m => m.Name).Select(m => new MemberDto(m.Id, m.ProjectId, m.UserId, m.Name, m.Role)).ToList();
    }

    public async Task<MemberDto> Handle(AddMember cmd, CancellationToken ct)
    {
        var p = await projects.GetAsync(cmd.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);
        var name = await people.RequireNameAsync(cmd.UserId, "userId", ct);
        if (await members.AnyAsync(m => m.ProjectId == p.Id && m.UserId == cmd.UserId, ct)) throw new ConflictException($"{name} is already on the team.");
        var m = new ProjectMember { ProjectId = p.Id, UserId = cmd.UserId, Name = name, Role = string.IsNullOrWhiteSpace(cmd.Role) ? null : cmd.Role.Trim() };
        members.Add(m);
        await members.SaveChangesAsync(ct);
        return new MemberDto(m.Id, m.ProjectId, m.UserId, m.Name, m.Role);
    }

    public async Task<Unit> Handle(RemoveMember cmd, CancellationToken ct)
    {
        var member = await members.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureProjectAsync(member.ProjectId, ct);
        members.Remove(member);
        await members.SaveChangesAsync(ct);
        return Unit.Value;
    }

    // ---- allocation
    public async Task<IReadOnlyList<AllocationDto>> Handle(ListAllocations q, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        return (await allocations.Query(new Specification<ProjectAllocation>(a => (q.ProjectId == null || a.ProjectId == q.ProjectId) && (q.UserId == null || a.UserId == q.UserId))).ToListAsync(ct))
            .Where(a => visible == null || visible.Contains(a.ProjectId))
            .OrderBy(a => a.FromDate).Select(a => new AllocationDto(a.Id, a.ProjectId, a.UserId, a.UserName, a.FromDate, a.ToDate, a.HoursPerWeek)).ToList();
    }

    public async Task<AllocationDto> Handle(SaveAllocation cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);
        var name = await people.RequireNameAsync(i.UserId, "userId", ct);
        ProjectAllocation a;
        if (cmd.Id is { } id) a = await allocations.GetForUpdateAsync(id, ct); else { a = new ProjectAllocation(); allocations.Add(a); }
        a.ProjectId = p.Id; a.UserId = i.UserId; a.UserName = name; a.FromDate = i.FromDate; a.ToDate = i.ToDate; a.HoursPerWeek = i.HoursPerWeek;
        await allocations.SaveChangesAsync(ct);
        return new AllocationDto(a.Id, a.ProjectId, a.UserId, a.UserName, a.FromDate, a.ToDate, a.HoursPerWeek);
    }

    public async Task<Unit> Handle(DeleteAllocation cmd, CancellationToken ct)
    {
        var allocation = await allocations.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureProjectAsync(allocation.ProjectId, ct);
        allocations.Remove(allocation);
        await allocations.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
