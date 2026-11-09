using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetEmployeeHistoryHandler(IEmployeeHistoryEntryRepository entries, ICurrentUser user) : IQueryHandler<GetEmployeeHistoryQuery, IReadOnlyList<EmployeeHistoryDto>>
{
    public async Task<IReadOnlyList<EmployeeHistoryDto>> Handle(GetEmployeeHistoryQuery query, CancellationToken ct)
    {
        var canSeePay = user.HasPermission(P.SalaryRead);
        var rows = await entries.ListForEmployeeNewestFirstAsync(query.EmployeeId, ct);
        return rows.Select(h => EmployeeHistoryDto.From(h, canSeePay)).ToList();
    }
}
