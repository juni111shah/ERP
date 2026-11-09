using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetPayrollHandler(IPayrollRunRepository runs) : IQueryHandler<GetPayrollQuery, PayrollDto>
{
    public async Task<PayrollDto> Handle(GetPayrollQuery query, CancellationToken ct) => PayrollDto.From(await runs.GetWithLinesAsync(query.Id, ct));
}
