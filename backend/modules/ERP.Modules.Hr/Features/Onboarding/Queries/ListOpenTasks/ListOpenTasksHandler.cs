using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListOpenTasksHandler(IEmployeeTaskRepository tasks, IEmployeeRepository employees, IClock clock) : IQueryHandler<ListOpenTasksQuery, IReadOnlyList<OpenTaskDto>>
{
    public async Task<IReadOnlyList<OpenTaskDto>> Handle(ListOpenTasksQuery query, CancellationToken ct)
    {
        var open = await tasks.ListOpenByDueDateAsync(ct);
        var ids = open.Select(x => x.EmployeeId).Distinct().ToList();
        var names = await employees.NamesByIdAsync(ids, ct);
        return open.Select(x => new OpenTaskDto(x.Id, x.EmployeeId, names.GetValueOrDefault(x.EmployeeId, ""), x.Kind, x.Title, x.DueDate, x.DueDate is { } d && d < DateOnly.FromDateTime(clock.UtcNow))).ToList();
    }
}
