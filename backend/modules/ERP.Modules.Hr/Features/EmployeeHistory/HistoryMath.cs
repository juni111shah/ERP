using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>The values of an employee that are tracked for history.</summary>
public sealed record EmployeeSnapshot(decimal Basic, decimal Gross, string Designation, string? Department, Guid? DepartmentId, Guid? PositionId, Guid? ManagerId, ContractType? Contract, EmployeeStatus Status)
{
    public static EmployeeSnapshot Of(Employee e) => new(e.BasicSalary, e.GrossMonthly, e.Designation, e.Department, e.DepartmentId, e.PositionId, e.ManagerEmployeeId, e.ContractType, e.Status);
}

public sealed record HistoryChange(HistoryKind Kind, string Summary, string? Old, string? New, bool IsPay);

/// <summary>What changed between two snapshots; no I/O.</summary>
public static class HistoryMath
{
    public static IReadOnlyList<HistoryChange> Diff(EmployeeSnapshot before, EmployeeSnapshot after)
    {
        var list = new List<HistoryChange>();
        if (before.Basic != after.Basic)
            list.Add(new HistoryChange(HistoryKind.Salary, after.Basic > before.Basic ? "Basic salary increased" : "Basic salary reduced", before.Basic.ToString("0.00"), after.Basic.ToString("0.00"), true));
        else if (before.Gross != after.Gross)
            list.Add(new HistoryChange(HistoryKind.Salary, "Allowances changed", before.Gross.ToString("0.00"), after.Gross.ToString("0.00"), true));
        if (!string.Equals(before.Designation, after.Designation, StringComparison.Ordinal))
            list.Add(new HistoryChange(HistoryKind.Designation, "Designation changed", before.Designation, after.Designation, false));
        if (before.DepartmentId != after.DepartmentId || !string.Equals(before.Department, after.Department, StringComparison.Ordinal))
            list.Add(new HistoryChange(HistoryKind.Department, "Department changed", before.Department, after.Department, false));
        if (before.PositionId != after.PositionId) list.Add(new HistoryChange(HistoryKind.Position, "Position changed", before.PositionId?.ToString(), after.PositionId?.ToString(), false));
        if (before.ManagerId != after.ManagerId) list.Add(new HistoryChange(HistoryKind.Manager, "Line manager changed", before.ManagerId?.ToString(), after.ManagerId?.ToString(), false));
        if (before.Contract != after.Contract) list.Add(new HistoryChange(HistoryKind.Contract, "Contract type changed", before.Contract?.ToString(), after.Contract?.ToString(), false));
        if (before.Status != after.Status && after.Status != EmployeeStatus.Terminated) list.Add(new HistoryChange(HistoryKind.Status, "Status changed", before.Status.ToString(), after.Status.ToString(), false));
        return list;
    }
}
