using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed class HrSearch(IReadRepository<Employee> employees, ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => HrModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        if (!user.HasPermission(P.EmployeesRead)) return [];
        return (await employees.Query(new Specification<Employee>(e => e.FullName.Contains(term) || e.Number.Contains(term) || e.Designation.Contains(term))).Take(limit).Select(e => new { e.Number, e.FullName, e.Designation }).ToListAsync(ct))
            .Select(e => new SearchHit("Employees", e.FullName, $"{e.Number} · {e.Designation}", $"/hr/employees?search={Uri.EscapeDataString(e.FullName)}")).ToList();
    }
}
