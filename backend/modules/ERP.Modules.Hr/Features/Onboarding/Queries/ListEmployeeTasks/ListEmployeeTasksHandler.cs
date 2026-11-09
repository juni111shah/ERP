using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListEmployeeTasksHandler(IEmployeeTaskRepository tasks, IClock clock) : IQueryHandler<ListEmployeeTasksQuery, IReadOnlyList<EmployeeTaskDto>>
{
    public async Task<IReadOnlyList<EmployeeTaskDto>> Handle(ListEmployeeTasksQuery query, CancellationToken ct)
        => (await tasks.ListForEmployeeAsync(query.EmployeeId, ct)).Select(x => EmployeeTaskDto.From(x, DateOnly.FromDateTime(clock.UtcNow))).ToList();
}
