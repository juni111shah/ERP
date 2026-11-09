using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetPayrollStatutoryHandler(IPayrollLineStatutoryRepository lines) : IQueryHandler<GetPayrollStatutoryQuery, IReadOnlyList<StatutoryTotalDto>>
{
    public async Task<IReadOnlyList<StatutoryTotalDto>> Handle(GetPayrollStatutoryQuery query, CancellationToken ct) => await lines.TotalsForRunAsync(query.RunId, ct);
}
