using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Copies an employee input onto an employee (create and update).</summary>
internal static class EmployeeMapping
{
    /// <summary>Copies the input onto the employee. Pay fields are only written by callers who may see pay (hr.salary.read); on update a missing pay value keeps the stored one.</summary>
    public static void Map(EmployeeInput i, Employee e, bool canWritePay)
    {
        e.FullName = i.FullName.Trim(); e.Designation = (i.Designation ?? e.Designation ?? "").Trim(); e.Department = i.Department?.Trim(); e.JoinDate = i.JoinDate; e.TerminationDate = i.TerminationDate;
        e.Email = i.Email?.Trim(); e.Phone = i.Phone?.Trim(); e.Nationality = i.Nationality?.Trim(); e.Status = i.Status;
        if (canWritePay)
        {
            e.BasicSalary = i.BasicSalary ?? e.BasicSalary; e.HousingAllowance = i.HousingAllowance ?? e.HousingAllowance; e.TransportAllowance = i.TransportAllowance ?? e.TransportAllowance;
            e.OtherAllowances = i.OtherAllowances ?? e.OtherAllowances; e.RecurringDeductions = i.RecurringDeductions ?? e.RecurringDeductions;
            e.Iban = string.IsNullOrWhiteSpace(i.Iban) ? null : i.Iban.Trim().ToUpperInvariant();
        }
        e.LabourCardNumber = string.IsNullOrWhiteSpace(i.LabourCardNumber) ? null : i.LabourCardNumber.Trim(); e.BankRoutingCode = string.IsNullOrWhiteSpace(i.BankRoutingCode) ? null : i.BankRoutingCode.Trim();
    }
}
