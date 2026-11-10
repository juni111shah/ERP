using ERP.Application.Abstractions;
using ERP.Application.Reminders;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Reminds HR managers when a probation period or fixed-term contract is about to end (30 and 7 days before). Reads the data directly: jobs run without a signed-in user.</summary>
public sealed class EmployeeContractReminders(IReadRepository<Employee> employees) : IReminderSource
{
    public string? ModuleKey => HrModule.Key;
    public static readonly int[] Stages = [30, 7];

    public static int? StageFor(int daysLeft) => daysLeft < 0 ? null : Stages.Where(s => daysLeft <= s).OrderBy(s => s).Cast<int?>().FirstOrDefault();

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var limit = today.AddDays(Stages.Max());
        var staff = await employees.Query(new Specification<Employee>(e => e.Status != EmployeeStatus.Terminated
            && ((e.ProbationEndDate != null && e.ProbationEndDate >= today && e.ProbationEndDate <= limit) || (e.ContractEndDate != null && e.ContractEndDate >= today && e.ContractEndDate <= limit)))).ToListAsync(ct);
        var list = new List<ReminderCandidate>();
        foreach (var e in staff)
        {
            foreach (var (label, date) in new[] { ("Probation", e.ProbationEndDate), ("Contract", e.ContractEndDate) })
            {
                if (date is not { } d || d < today || d > limit) continue;
                var left = d.DayNumber - today.DayNumber;
                if (StageFor(left) is not { } stage) continue;
                list.Add(new ReminderCandidate($"hr-contract:{e.Id}:{label}:{d:yyyyMMdd}:{stage}", $"{label} of {e.FullName} ends in {left} day(s)",
                    $"{label} of {e.Number} {e.FullName} ends on {d:dd MMM yyyy}.", "/hr/employees", P.EmployeesManage));
            }
        }
        return list;
    }
}
