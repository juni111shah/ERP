using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Builds the employee detail, hiding pay unless the caller may see it.</summary>
public sealed class EmployeeDtoFactory(
    IReadRepository<LegalEntity> legalEntities, IReadRepository<Employee> employees, IReadRepository<LeaveRequest> leave, GratuityCalculator gratuity, ICustomFieldService fields,
    IUserDirectory users, ICurrentUser user, IClock clock, ISettingsService settings)
{
    public async Task<EmployeeDto> BuildAsync(Employee e, bool includePay, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var service = gratuity.Calculate(e.BasicSalary, e.JoinDate, e.TerminationDate, today, await CountryAsync(e.LegalEntityId, ct), await settings.GetAsync<int?>("hr.daysBasis", e.LegalEntityId, ct) ?? 30);
        string? iban = null;
        if (includePay) iban = e.Iban is { Length: > 4 } v && !user.HasPermission(CorePermissions.SensitiveRead) ? new string('*', v.Length - 4) + v[^4..] : e.Iban;
        string? userName = null;
        if (e.UserId is { } uid) userName = (await users.ListMembersAsync([uid], ct)).FirstOrDefault()?.FullName;
        string? managerName = null;
        if (e.ManagerEmployeeId is { } mid) managerName = (await employees.FindAsync(mid, ct))?.FullName;
        var id = e.Id;
        var onLeave = await leave.AnyAsync(r => r.EmployeeId == id && r.Status == LeaveStatus.Approved && r.FromDate <= today && r.ToDate >= today, ct);
        return new EmployeeDto(e.Id, e.Number, e.LegalEntityId, e.FullName, e.Designation, e.Department, e.JoinDate, e.TerminationDate, e.Email, e.Phone, e.Nationality, e.Status,
            includePay ? e.BasicSalary : null, includePay ? e.HousingAllowance : null, includePay ? e.TransportAllowance : null, includePay ? e.OtherAllowances : null, includePay ? e.RecurringDeductions : null,
            iban, includePay ? e.GrossMonthly : null, service.ServiceYears, includePay ? service.Amount : null, await fields.ReadAsync(Docs.Employee, e, ct), e.LabourCardNumber, e.BankRoutingCode,
            e.UserId, userName, e.ManagerEmployeeId, managerName, e.DepartmentId, e.PositionId, e.DateOfBirth, e.Gender, e.MaritalStatus, e.EmergencyContactName, e.EmergencyContactPhone, e.Address,
            e.ContractType, e.ProbationEndDate, e.ContractEndDate, e.NoticePeriodDays, e.BankName, onLeave);
    }

    private async Task<string> CountryAsync(Guid legalEntityId, CancellationToken ct)
        => await legalEntities.Query(new Specification<LegalEntity>(l => l.Id == legalEntityId)).Select(l => l.CountryCode).FirstOrDefaultAsync(ct) ?? "";
}
