using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListShiftsHandler(IReadRepository<Shift> shifts, IEmployeeRepository employees) : IQueryHandler<ListShiftsQuery, IReadOnlyList<ShiftDto>>
{
    public async Task<IReadOnlyList<ShiftDto>> Handle(ListShiftsQuery query, CancellationToken ct)
    {
        var all = await shifts.ListAsync(ct: ct);
        var counts = await employees.CountByShiftAsync(ct);
        return all.OrderBy(s => s.Code).Select(s => ShiftDto.From(s, counts.GetValueOrDefault(s.Id))).ToList();
    }
}
