using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class EmployeeRepository(IRepository<Employee> inner) : RepositoryBase<Employee>(inner), IEmployeeRepository
{
    public Task<List<OrgPersonSource>> ListOrgPeopleAsync(CancellationToken ct)
        => Query(new Specification<Employee>(e => e.Status != EmployeeStatus.Terminated))
            .Select(e => new OrgPersonSource(e.Id, e.Number, e.FullName, e.Designation, e.DepartmentId, e.ManagerEmployeeId)).ToListAsync(ct);

    public async Task<Dictionary<Guid, string?>> IbansByIdAsync(List<Guid> ids, CancellationToken ct)
        => (await Query(new Specification<Employee>(e => ids.Contains(e.Id))).Select(e => new { e.Id, e.Iban }).ToListAsync(ct)).ToDictionary(e => e.Id, e => e.Iban);

    public async Task<Dictionary<Guid, Employee>> MapByIdsAsync(List<Guid> ids, CancellationToken ct)
        => (await Query(new Specification<Employee>(e => ids.Contains(e.Id))).ToListAsync(ct)).ToDictionary(e => e.Id);

    public Task<List<Employee>> ListOfEntityAsync(Guid legalEntityId, CancellationToken ct)
        => Query(new Specification<Employee>(e => e.LegalEntityId == legalEntityId)).ToListAsync(ct);

    public Task<List<EmployeeDepartment>> ListDepartmentsAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken ct)
        => Query(new Specification<Employee>(e => employeeIds.Contains(e.Id))).Select(e => new EmployeeDepartment(e.Id, e.DepartmentId)).ToListAsync(ct);

    public async Task<string> GetNameAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<Employee>(e => e.Id == id)).Select(e => e.FullName).FirstOrDefaultAsync(ct) ?? "";

    public async Task<List<Employee>> ListNotTerminatedJoinedByAsync(Guid? legalEntityId, DateOnly asOf, CancellationToken ct)
        => (await Query(new Specification<Employee>(e => (legalEntityId == null || e.LegalEntityId == legalEntityId) && e.JoinDate <= asOf)).ToListAsync(ct))
            .Where(e => e.Status != EmployeeStatus.Terminated).OrderBy(e => e.Number).ToList();

    public Task<EmployeePayslipDetails?> FindPayslipDetailsAsync(Guid id, CancellationToken ct)
        => Query(new Specification<Employee>(e => e.Id == id)).Select(e => new EmployeePayslipDetails(e.Designation, e.Department, e.Email)).FirstOrDefaultAsync(ct);

    public async Task<List<Employee>> ListPayableInAsync(Guid? legalEntityId, Guid? employeeId, DateOnly from, DateOnly to, CancellationToken ct)
        => (await Query(new Specification<Employee>(e => (legalEntityId == null || e.LegalEntityId == legalEntityId) && (employeeId == null || e.Id == employeeId))).ToListAsync(ct))
            .Where(e => e.IsPayableIn(from, to)).OrderBy(e => e.Number).ToList();

    public Task<Employee?> FindByUserAsync(Guid userId, CancellationToken ct)
        => Query(new Specification<Employee>(e => e.UserId == userId)).FirstOrDefaultAsync(ct);

    public Task<List<Guid>> ListReportIdsAsync(Guid managerEmployeeId, CancellationToken ct)
        => Query(new Specification<Employee>(e => e.ManagerEmployeeId == managerEmployeeId)).Select(e => e.Id).ToListAsync(ct);

    public Task<Employee?> FindByNumberAsync(string number, CancellationToken ct)
        => Query(new Specification<Employee>(e => e.Number == number)).FirstOrDefaultAsync(ct);

    public Task<List<Employee>> ListByIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Employee>(e => ids.Contains(e.Id))).ToListAsync(ct);

    public Task<List<Employee>> ListByIdsForUpdateAsync(List<Guid> ids, CancellationToken ct)
        => Tracked(new Specification<Employee>(e => ids.Contains(e.Id))).ToListAsync(ct);

    public Task<int> CountOnShiftAsync(Guid shiftId, CancellationToken ct)
        => Query(new Specification<Employee>(e => e.ShiftId == shiftId)).CountAsync(ct);

    public async Task<Dictionary<Guid, int>> CountByShiftAsync(CancellationToken ct)
        => (await Query(new Specification<Employee>(e => e.ShiftId != null)).GroupBy(e => e.ShiftId!.Value).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Id, x => x.N);

    public async Task<Dictionary<Guid, EmployeeLabel>> MapLabelsOfNotTerminatedAsync(List<Guid> ids, CancellationToken ct)
        => (await Query(new Specification<Employee>(e => ids.Contains(e.Id) && e.Status != EmployeeStatus.Terminated)).Select(e => new EmployeeLabel(e.Id, e.Number, e.FullName)).ToListAsync(ct)).ToDictionary(e => e.Id);

    public async Task<Dictionary<Guid, string>> NamesByIdAsync(List<Guid> ids, CancellationToken ct)
        => (await Query(new Specification<Employee>(e => ids.Contains(e.Id))).Select(e => new { e.Id, e.FullName }).ToListAsync(ct)).ToDictionary(e => e.Id, e => e.FullName);

    public Task<List<EmployeeReportRow>> ListReportRowsAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Employee>(e => legalEntityId == null || e.LegalEntityId == legalEntityId))
            .Select(e => new EmployeeReportRow(e.Id, e.Department, e.Nationality, e.JoinDate, e.TerminationDate, e.Status, e.ContractType, e.CustomFields)).ToListAsync(ct);

    public Task<List<EmployeeOccasionRow>> ListOccasionRowsAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Employee>(e => (legalEntityId == null || e.LegalEntityId == legalEntityId) && e.Status != EmployeeStatus.Terminated))
            .Select(e => new EmployeeOccasionRow(e.Id, e.Number, e.FullName, e.JoinDate, e.DateOfBirth, e.CustomFields)).ToListAsync(ct);
}
