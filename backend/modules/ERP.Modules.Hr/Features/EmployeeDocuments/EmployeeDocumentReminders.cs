using ERP.Application.Abstractions;
using ERP.Application.Reminders;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Reminds HR managers once at 60, 30 and 7 days before a document expires. Reads the data directly: jobs run without a signed-in user.</summary>
public sealed class EmployeeDocumentReminders(IReadRepository<EmployeeDocument> documents, IReadRepository<Employee> employees) : IReminderSource
{
    public string? ModuleKey => HrModule.Key;

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var limit = today.AddDays(DocumentExpiry.ReminderDays.Max());
        var due = await documents.Query(new Specification<EmployeeDocument>(d => d.ExpiryDate != null && d.ExpiryDate >= today && d.ExpiryDate <= limit)).ToListAsync(ct);
        if (due.Count == 0) return [];
        var ids = due.Select(d => d.EmployeeId).Distinct().ToList();
        var staff = (await employees.Query(new Specification<Employee>(e => ids.Contains(e.Id) && e.Status != EmployeeStatus.Terminated)).Select(e => new { e.Id, e.Number, e.FullName }).ToListAsync(ct)).ToDictionary(e => e.Id);
        var list = new List<ReminderCandidate>();
        foreach (var d in due)
        {
            if (!staff.TryGetValue(d.EmployeeId, out var e)) continue;
            var left = d.ExpiryDate!.Value.DayNumber - today.DayNumber;
            if (DocumentExpiry.ReminderStage(left) is not { } stage) continue;
            list.Add(new ReminderCandidate($"hr-doc-expiry:{d.Id}:{d.ExpiryDate:yyyyMMdd}:{stage}", $"{d.Type} expires in {left} day(s): {e.Number} {e.FullName}",
                $"{d.Type}{(d.Number is null ? "" : " " + d.Number)} of {e.FullName} expires on {d.ExpiryDate:dd MMM yyyy}.", "/hr/employees", P.EmployeesManage));
        }
        return list;
    }
}
