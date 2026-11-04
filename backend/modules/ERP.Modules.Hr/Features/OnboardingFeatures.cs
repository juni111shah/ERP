using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record ChecklistItemInput(string Title, int DueOffsetDays = 0);
public sealed record ChecklistTemplateInput(string Name, ChecklistKind Kind, bool IsActive = true, IReadOnlyList<ChecklistItemInput>? Items = null);
public sealed record ChecklistTemplateDto(Guid Id, string Name, ChecklistKind Kind, bool IsActive, IReadOnlyList<ChecklistItemInput> Items);
public sealed record ChecklistTemplateRow(Guid Id, string Name, ChecklistKind Kind, bool IsActive);
public sealed record EmployeeTaskInput(string Title, DateOnly? DueDate = null, ChecklistKind Kind = ChecklistKind.Onboarding, string? Notes = null);
public sealed record EmployeeTaskDto(Guid Id, Guid EmployeeId, ChecklistKind Kind, string Title, DateOnly? DueDate, bool IsDone, DateTime? DoneAt, string? Notes, bool Overdue);
/// <summary>An open task of any employee, for the HR to-do list.</summary>
public sealed record OpenTaskDto(Guid Id, Guid EmployeeId, string EmployeeName, ChecklistKind Kind, string Title, DateOnly? DueDate, bool Overdue);

public sealed record GetChecklistTemplate(Guid Id) : IQuery<ChecklistTemplateDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
public sealed record CreateChecklistTemplate(ChecklistTemplateInput Input) : ICommand<ChecklistTemplateDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record UpdateChecklistTemplate(Guid Id, ChecklistTemplateInput Input) : ICommand<ChecklistTemplateDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record DeleteChecklistTemplate(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
/// <summary>Creates the template's steps as tasks of the employee. A template can be applied once per employee.</summary>
public sealed record ApplyChecklist(Guid EmployeeId, Guid TemplateId) : ICommand<IReadOnlyList<EmployeeTaskDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record ListEmployeeTasks(Guid EmployeeId) : IQuery<IReadOnlyList<EmployeeTaskDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
public sealed record AddEmployeeTask(Guid EmployeeId, EmployeeTaskInput Input) : ICommand<EmployeeTaskDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record SetEmployeeTaskDone(Guid TaskId, bool Done) : ICommand<EmployeeTaskDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record DeleteEmployeeTask(Guid TaskId) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record ListOpenTasks() : IQuery<IReadOnlyList<OpenTaskDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }

public sealed class ChecklistTemplateInputValidator : AbstractValidator<ChecklistTemplateInput>
{
    public ChecklistTemplateInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Kind).IsInEnum();
        RuleForEach(x => x.Items).ChildRules(i => { i.RuleFor(y => y.Title).NotEmpty().MaximumLength(200); i.RuleFor(y => y.DueOffsetDays).InclusiveBetween(-365, 365); });
    }
}
public sealed class CreateChecklistTemplateValidator : AbstractValidator<CreateChecklistTemplate> { public CreateChecklistTemplateValidator() => RuleFor(x => x.Input).SetValidator(new ChecklistTemplateInputValidator()); }
public sealed class UpdateChecklistTemplateValidator : AbstractValidator<UpdateChecklistTemplate> { public UpdateChecklistTemplateValidator() => RuleFor(x => x.Input).SetValidator(new ChecklistTemplateInputValidator()); }
public sealed class AddEmployeeTaskValidator : AbstractValidator<AddEmployeeTask>
{
    public AddEmployeeTaskValidator() { RuleFor(x => x.Input.Title).NotEmpty().MaximumLength(200); RuleFor(x => x.Input.Notes).MaximumLength(500); }
}

internal sealed class OnboardingHandlers(
    IRepository<ChecklistTemplate> templates, IRepository<ChecklistTemplateItem> templateItems, IRepository<EmployeeTask> tasks, IReadRepository<Employee> employees, IClock clock, ICurrentUser user) :
    IRequestHandler<GetChecklistTemplate, ChecklistTemplateDto>,
    IRequestHandler<CreateChecklistTemplate, ChecklistTemplateDto>,
    IRequestHandler<UpdateChecklistTemplate, ChecklistTemplateDto>,
    IRequestHandler<DeleteChecklistTemplate, Unit>,
    IRequestHandler<ApplyChecklist, IReadOnlyList<EmployeeTaskDto>>,
    IRequestHandler<ListEmployeeTasks, IReadOnlyList<EmployeeTaskDto>>,
    IRequestHandler<AddEmployeeTask, EmployeeTaskDto>,
    IRequestHandler<SetEmployeeTaskDone, EmployeeTaskDto>,
    IRequestHandler<DeleteEmployeeTask, Unit>,
    IRequestHandler<ListOpenTasks, IReadOnlyList<OpenTaskDto>>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<ChecklistTemplateDto> Handle(GetChecklistTemplate q, CancellationToken ct)
        => ToDto(await templates.Query(new Specification<ChecklistTemplate>(t => t.Id == q.Id).Include(t => t.Items)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException());

    public async Task<ChecklistTemplateDto> Handle(CreateChecklistTemplate cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var name = i.Name.Trim();
        if (await templates.AnyAsync(t => t.Name == name, ct)) throw new ConflictException($"A checklist named '{name}' already exists.");
        var t = new ChecklistTemplate { Name = name, Kind = i.Kind, IsActive = i.IsActive };
        templates.Add(t);
        AddItems(t, i);
        await templates.SaveChangesAsync(ct);
        return ToDto(t);
    }

    public async Task<ChecklistTemplateDto> Handle(UpdateChecklistTemplate cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var t = await templates.Tracked(new Specification<ChecklistTemplate>(x => x.Id == cmd.Id).Include(x => x.Items)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var name = i.Name.Trim();
        if (name != t.Name && await templates.AnyAsync(x => x.Name == name && x.Id != t.Id, ct)) throw new ConflictException($"A checklist named '{name}' already exists.");
        t.Name = name; t.Kind = i.Kind; t.IsActive = i.IsActive;
        if (i.Items is not null)
        {
            foreach (var old in t.Items.ToList()) templateItems.Remove(old);
            t.Items.Clear();
            AddItems(t, i);
        }
        await templates.SaveChangesAsync(ct);
        return ToDto(t);
    }

    public async Task<Unit> Handle(DeleteChecklistTemplate cmd, CancellationToken ct)
    {
        var t = await templates.Tracked(new Specification<ChecklistTemplate>(x => x.Id == cmd.Id).Include(x => x.Items)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        foreach (var item in t.Items.ToList()) templateItems.Remove(item);
        templates.Remove(t);
        await templates.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<IReadOnlyList<EmployeeTaskDto>> Handle(ApplyChecklist cmd, CancellationToken ct)
    {
        var e = await employees.FindAsync(cmd.EmployeeId, ct) ?? throw new NotFoundException();
        var t = await templates.Query(new Specification<ChecklistTemplate>(x => x.Id == cmd.TemplateId).Include(x => x.Items)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (!t.IsActive) throw new ConflictException("This checklist is switched off.");
        var anchor = t.Kind == ChecklistKind.Onboarding ? e.JoinDate : e.TerminationDate ?? Today;
        var already = (await tasks.Query(new Specification<EmployeeTask>(x => x.EmployeeId == e.Id && x.Kind == t.Kind)).Select(x => x.Title).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var created = new List<EmployeeTask>();
        foreach (var item in t.Items.OrderBy(x => x.Position))
        {
            if (already.Contains(item.Title)) continue;
            var task = new EmployeeTask { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, Kind = t.Kind, Title = item.Title, DueDate = anchor.AddDays(item.DueOffsetDays) };
            tasks.Add(task); created.Add(task);
        }
        await tasks.SaveChangesAsync(ct);
        return created.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<EmployeeTaskDto>> Handle(ListEmployeeTasks q, CancellationToken ct)
        => (await tasks.Query(new Specification<EmployeeTask>(x => x.EmployeeId == q.EmployeeId)).ToListAsync(ct)).OrderBy(x => x.IsDone).ThenBy(x => x.DueDate).Select(ToDto).ToList();

    public async Task<EmployeeTaskDto> Handle(AddEmployeeTask cmd, CancellationToken ct)
    {
        var e = await employees.FindAsync(cmd.EmployeeId, ct) ?? throw new NotFoundException();
        var task = new EmployeeTask { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, Kind = cmd.Input.Kind, Title = cmd.Input.Title.Trim(), DueDate = cmd.Input.DueDate, Notes = cmd.Input.Notes?.Trim() };
        tasks.Add(task);
        await tasks.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task<EmployeeTaskDto> Handle(SetEmployeeTaskDone cmd, CancellationToken ct)
    {
        var task = await tasks.GetForUpdateAsync(cmd.TaskId, ct);
        task.IsDone = cmd.Done; task.DoneAt = cmd.Done ? clock.UtcNow : null; task.DoneByUserId = cmd.Done ? user.UserId : null;
        await tasks.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task<Unit> Handle(DeleteEmployeeTask cmd, CancellationToken ct)
    {
        tasks.Remove(await tasks.GetForUpdateAsync(cmd.TaskId, ct));
        await tasks.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<IReadOnlyList<OpenTaskDto>> Handle(ListOpenTasks q, CancellationToken ct)
    {
        var open = (await tasks.Query(new Specification<EmployeeTask>(x => !x.IsDone)).ToListAsync(ct)).OrderBy(x => x.DueDate ?? DateOnly.MaxValue).ToList();
        var ids = open.Select(x => x.EmployeeId).Distinct().ToList();
        var names = (await employees.Query(new Specification<Employee>(e => ids.Contains(e.Id))).Select(e => new { e.Id, e.FullName }).ToListAsync(ct)).ToDictionary(e => e.Id, e => e.FullName);
        return open.Select(x => new OpenTaskDto(x.Id, x.EmployeeId, names.GetValueOrDefault(x.EmployeeId, ""), x.Kind, x.Title, x.DueDate, x.DueDate is { } d && d < Today)).ToList();
    }

    private static void AddItems(ChecklistTemplate t, ChecklistTemplateInput i)
    {
        var n = 0;
        foreach (var item in i.Items ?? []) t.Items.Add(new ChecklistTemplateItem { TemplateId = t.Id, Position = ++n, Title = item.Title.Trim(), DueOffsetDays = item.DueOffsetDays });
    }

    private static ChecklistTemplateDto ToDto(ChecklistTemplate t) => new(t.Id, t.Name, t.Kind, t.IsActive, t.Items.OrderBy(x => x.Position).Select(x => new ChecklistItemInput(x.Title, x.DueOffsetDays)).ToList());
    private EmployeeTaskDto ToDto(EmployeeTask x) => new(x.Id, x.EmployeeId, x.Kind, x.Title, x.DueDate, x.IsDone, x.DoneAt, x.Notes, !x.IsDone && x.DueDate is { } d && d < Today);
}
