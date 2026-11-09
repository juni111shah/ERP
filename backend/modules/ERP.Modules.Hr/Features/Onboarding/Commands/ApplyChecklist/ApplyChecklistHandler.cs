using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ApplyChecklistHandler(IChecklistTemplateRepository templates, IEmployeeTaskRepository tasks, IReadRepository<Employee> employees, IClock clock)
    : ICommandHandler<ApplyChecklistCommand, IReadOnlyList<EmployeeTaskDto>>
{
    public async Task<IReadOnlyList<EmployeeTaskDto>> Handle(ApplyChecklistCommand command, CancellationToken ct)
    {
        // 1. The employee and an active template.
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var e = await employees.FindAsync(command.EmployeeId, ct) ?? throw new NotFoundException();
        var t = await templates.GetWithItemsAsync(command.TemplateId, ct);
        if (!t.IsActive) throw new ConflictException("This checklist is switched off.");

        // 2. A task per step the employee does not have yet, due relative to joining (onboarding) or leaving.
        var anchor = t.Kind == ChecklistKind.Onboarding ? e.JoinDate : e.TerminationDate ?? today;
        var already = await tasks.TitlesOfKindAsync(e.Id, t.Kind, ct);
        var created = new List<EmployeeTask>();
        foreach (var item in t.Items.OrderBy(x => x.Position))
        {
            if (already.Contains(item.Title)) continue;
            var task = new EmployeeTask { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, Kind = t.Kind, Title = item.Title, DueDate = anchor.AddDays(item.DueOffsetDays) };
            tasks.Add(task); created.Add(task);
        }

        // 3. Save.
        await tasks.SaveChangesAsync(ct);
        return created.Select(x => EmployeeTaskDto.From(x, DateOnly.FromDateTime(clock.UtcNow))).ToList();
    }
}
