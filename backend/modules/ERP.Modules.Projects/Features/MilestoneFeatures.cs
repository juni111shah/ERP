using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed record MilestoneInput(
    Guid ProjectId,
    string Title,
    DateOnly DueDate,
    decimal Amount,
    MilestoneStatus Status = MilestoneStatus.Pending,
    DateOnly? AchievedDate = null);

public sealed record MilestoneDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Title,
    DateOnly DueDate,
    decimal Amount,
    MilestoneStatus Status,
    DateOnly? AchievedDate,
    DateTime CreatedAt);

public sealed record GetMilestone(Guid Id) : IQuery<MilestoneDto>, IRequirePermission { public string RequiredPermission => P.MilestonesRead; }
public sealed record CreateMilestone(MilestoneInput Input) : ICommand<MilestoneDto>, IRequirePermission { public string RequiredPermission => P.MilestonesManage; }
public sealed record UpdateMilestone(Guid Id, MilestoneInput Input) : ICommand<MilestoneDto>, IRequirePermission { public string RequiredPermission => P.MilestonesManage; }
public sealed record DeleteMilestone(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.MilestonesManage; }
public sealed record AchieveMilestone(Guid Id, DateOnly? AchievedDate = null) : ICommand<MilestoneDto>, IRequirePermission { public string RequiredPermission => P.MilestonesManage; }

public sealed class MilestoneInputValidator : AbstractValidator<MilestoneInput>
{
    public MilestoneInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class CreateMilestoneValidator : AbstractValidator<CreateMilestone>
{
    public CreateMilestoneValidator() => RuleFor(x => x.Input).SetValidator(new MilestoneInputValidator());
}

public sealed class UpdateMilestoneValidator : AbstractValidator<UpdateMilestone>
{
    public UpdateMilestoneValidator() => RuleFor(x => x.Input).SetValidator(new MilestoneInputValidator());
}

internal sealed class MilestoneHandlers(
    IRepository<ProjectMilestone> milestones,
    IReadRepository<Project> projects) :
    IRequestHandler<GetMilestone, MilestoneDto>,
    IRequestHandler<CreateMilestone, MilestoneDto>,
    IRequestHandler<UpdateMilestone, MilestoneDto>,
    IRequestHandler<DeleteMilestone, Unit>,
    IRequestHandler<AchieveMilestone, MilestoneDto>
{
    public async Task<MilestoneDto> Handle(GetMilestone q, CancellationToken ct)
    {
        var m = await milestones.GetAsync(q.Id, ct);
        return await BuildDtoAsync(m, ct);
    }

    public async Task<MilestoneDto> Handle(CreateMilestone cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        if (i.Status == MilestoneStatus.Invoiced) throw new FieldValidationException("status", "A milestone becomes Invoiced only when its project is billed.");
        var m = new ProjectMilestone
        {
            ProjectId = p.Id,
            Title = i.Title.Trim(),
            DueDate = i.DueDate,
            Amount = i.Amount,
            Status = i.Status,
            AchievedDate = AchievedDateFor(i),
        };
        milestones.Add(m);
        await milestones.SaveChangesAsync(ct);
        return await BuildDtoAsync(m, ct);
    }

    public async Task<MilestoneDto> Handle(UpdateMilestone cmd, CancellationToken ct)
    {
        var m = await milestones.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        if (m.Status == MilestoneStatus.Invoiced) throw new ConflictException("An invoiced milestone cannot be edited.");
        if (i.Status != m.Status) ProjectsTransitions.Milestone.Ensure(m.Status, i.Status, isSystem: false);
        var achieved = AchievedDateFor(i);
        m.Title = i.Title.Trim();
        m.DueDate = i.DueDate;
        m.Amount = i.Amount;
        m.Status = i.Status;
        m.AchievedDate = achieved;

        await milestones.SaveChangesAsync(ct);
        return await BuildDtoAsync(m, ct);
    }

    public async Task<Unit> Handle(DeleteMilestone cmd, CancellationToken ct)
    {
        var m = await milestones.GetForUpdateAsync(cmd.Id, ct);
        if (m.Status == MilestoneStatus.Invoiced) throw new ConflictException("An invoiced milestone cannot be deleted.");
        milestones.Remove(m);
        await milestones.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<MilestoneDto> Handle(AchieveMilestone cmd, CancellationToken ct)
    {
        var m = await milestones.GetForUpdateAsync(cmd.Id, ct);
        ProjectsTransitions.Milestone.Ensure(m.Status, MilestoneStatus.Achieved, isSystem: false);
        var achieved = cmd.AchievedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (achieved > DateOnly.FromDateTime(DateTime.UtcNow)) throw new FieldValidationException("achievedDate", "The achieved date cannot be in the future.");
        m.Status = MilestoneStatus.Achieved;
        m.AchievedDate = achieved;
        await milestones.SaveChangesAsync(ct);
        return await BuildDtoAsync(m, ct);
    }

    /// <summary>Pending milestones carry no achieved date; achieved ones default to today and cannot be dated in the future.</summary>
    private static DateOnly? AchievedDateFor(MilestoneInput i)
    {
        if (i.Status == MilestoneStatus.Pending) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var date = i.AchievedDate ?? today;
        if (date > today) throw new FieldValidationException("achievedDate", "The achieved date cannot be in the future.");
        return date;
    }

    private async Task<MilestoneDto> BuildDtoAsync(ProjectMilestone m, CancellationToken ct)
    {
        var p = await projects.GetAsync(m.ProjectId, ct);
        return new MilestoneDto(
            m.Id,
            m.ProjectId,
            p.Name,
            m.Title,
            m.DueDate,
            m.Amount,
            m.Status,
            m.AchievedDate,
            m.CreatedAt);
    }
}
