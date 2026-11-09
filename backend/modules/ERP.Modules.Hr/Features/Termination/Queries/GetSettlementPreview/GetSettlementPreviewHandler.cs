using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class GetSettlementPreviewHandler(IReadRepository<Employee> employees, SettlementCalculator settlement) : IQueryHandler<GetSettlementPreviewQuery, SettlementPreviewDto>
{
    public async Task<SettlementPreviewDto> Handle(GetSettlementPreviewQuery query, CancellationToken ct)
    {
        var e = await employees.FindAsync(query.EmployeeId, ct) ?? throw new NotFoundException();
        if (query.TerminationDate < e.JoinDate) throw new FieldValidationException("terminationDate", "The termination date cannot be before the join date.");
        return await settlement.BuildAsync(e, query.TerminationDate, e.LastWorkingDay, query.NoticeGivenOn ?? e.NoticeGivenOn, ct);
    }
}
