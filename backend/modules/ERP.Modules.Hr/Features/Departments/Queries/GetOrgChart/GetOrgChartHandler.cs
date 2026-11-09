using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetOrgChartHandler(IDepartmentRepository departments, IEmployeeRepository employees) : IQueryHandler<GetOrgChartQuery, OrgChartDto>
{
    public async Task<OrgChartDto> Handle(GetOrgChartQuery query, CancellationToken ct)
    {
        // 1. Every department, and every employee not terminated.
        var depts = await departments.ListOrgSourcesAsync(ct);
        var people = await employees.ListOrgPeopleAsync(ct);

        // 2. Build both trees.
        return OrgChartBuilder.Build(depts, people);
    }
}
