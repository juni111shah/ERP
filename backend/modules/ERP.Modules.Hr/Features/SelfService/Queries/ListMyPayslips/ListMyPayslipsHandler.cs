using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListMyPayslipsHandler(MyEmployee me, IPayrollRunRepository runs) : IQueryHandler<ListMyPayslipsQuery, IReadOnlyList<MyPayslipDto>>
{
    public async Task<IReadOnlyList<MyPayslipDto>> Handle(ListMyPayslipsQuery query, CancellationToken ct)
    {
        var mine = (await me.GetAsync(ct)).Id;
        return (await runs.ListPayslipsOfAsync(mine, null, ct)).OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToList();
    }
}
