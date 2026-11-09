using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetMyPayslipHandler(MyEmployee me, IPayrollRunRepository runs) : IQueryHandler<GetMyPayslipQuery, MyPayslipDto>
{
    public async Task<MyPayslipDto> Handle(GetMyPayslipQuery query, CancellationToken ct)
    {
        var mine = (await me.GetAsync(ct)).Id;
        return (await runs.ListPayslipsOfAsync(mine, query.LineId, ct)).FirstOrDefault() ?? throw new NotFoundException("Payslip not found.");
    }
}
