using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

internal sealed class GetMyEmployeeHandler(MyEmployee me, EmployeeDtoFactory dtos) : IQueryHandler<GetMyEmployeeQuery, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(GetMyEmployeeQuery query, CancellationToken ct) => await dtos.BuildAsync(await me.GetAsync(ct), includePay: true, ct);
}
