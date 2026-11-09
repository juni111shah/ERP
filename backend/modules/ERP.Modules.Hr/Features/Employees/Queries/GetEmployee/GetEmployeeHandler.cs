using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class GetEmployeeHandler(IReadRepository<Employee> employees, ICurrentUser user, EmployeeDtoFactory dtos) : IQueryHandler<GetEmployeeQuery, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(GetEmployeeQuery query, CancellationToken ct)
        => await dtos.BuildAsync(await employees.GetAsync(query.Id, ct), user.HasPermission(P.SalaryRead), ct);
}
