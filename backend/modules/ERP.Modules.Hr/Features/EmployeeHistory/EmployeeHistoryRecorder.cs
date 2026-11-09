using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Writes history entries; called from the employee handlers.</summary>
public sealed class EmployeeHistoryRecorder(IRepository<EmployeeHistoryEntry> entries, IClock clock, ICurrentUser user)
{
    public DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public void Add(Employee e, HistoryKind kind, string summary, string? oldValue, string? newValue, bool isPay, DateOnly? date = null)
        => entries.Add(new EmployeeHistoryEntry { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, EffectiveDate = date ?? Today, Kind = kind, Summary = summary, OldValue = oldValue, NewValue = newValue, IsPay = isPay, ChangedByUserId = user.UserId });

    public void Joined(Employee e) => Add(e, HistoryKind.Joined, "Joined", null, e.Designation, false, e.JoinDate);

    public void Changed(Employee e, EmployeeSnapshot before)
    {
        foreach (var c in HistoryMath.Diff(before, EmployeeSnapshot.Of(e))) Add(e, c.Kind, c.Summary, c.Old, c.New, c.IsPay);
    }

    public void Terminated(Employee e, DateOnly date, string? reason) => Add(e, HistoryKind.Terminated, string.IsNullOrWhiteSpace(reason) ? "Employment ended" : $"Employment ended: {reason.Trim()}", null, null, false, date);
}
