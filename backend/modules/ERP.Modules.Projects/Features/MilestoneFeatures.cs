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
        var m = new ProjectMilestone
        {
            ProjectId = p.Id,
            Title = i.Title.Trim(),
            DueDate = i.DueDate,
            Amount = i.Amount,
            Status = i.Status,
            AchievedDate = i.AchievedDate,
        };
        milestones.Add(m);
        await milestones.SaveChangesAsync(ct);
        return await BuildDtoAsync(m, ct);
    }

    public async Task<MilestoneDto> Handle(UpdateMilestone cmd, CancellationToken ct)
    {
        var m = await milestones.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        ProjectsTransitions.Milestone.Ensure(m.Status, i.Status, isSystem: false);
        m.Title = i.Title.Trim();
        m.DueDate = i.DueDate;
        m.Amount = i.Amount;
        m.Status = i.Status;
        m.AchievedDate = i.AchievedDate;

        await milestones.SaveChangesAsync(ct);
        return await BuildDtoAsync(m, ct);
    }

    public async Task<Unit> Handle(DeleteMilestone cmd, CancellationToken ct)
    {
        var m = await milestones.GetForUpdateAsync(cmd.Id, ct);
        milestones.Remove(m);
        await milestones.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<MilestoneDto> Handle(AchieveMilestone cmd, CancellationToken ct)
    {
        var m = await milestones.GetForUpdateAsync(cmd.Id, ct);
        ProjectsTransitions.Milestone.Ensure(m.Status, MilestoneStatus.Achieved, isSystem: false);
        m.Status = MilestoneStatus.Achieved;
        m.AchievedDate = cmd.AchievedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        await milestones.SaveChangesAsync(ct);
        return await BuildDtoAsync(m, ct);
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
