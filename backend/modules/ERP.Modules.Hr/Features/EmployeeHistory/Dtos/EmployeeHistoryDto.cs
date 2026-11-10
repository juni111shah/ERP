using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record HistoryNoteInput(string Summary, DateOnly? EffectiveDate = null);

/// <summary>Old and new values of pay entries are null for callers without hr.salary.read.</summary>
public sealed record EmployeeHistoryDto(Guid Id, DateOnly EffectiveDate, HistoryKind Kind, string Summary, string? OldValue, string? NewValue, bool IsPay, Guid? ChangedByUserId)
{
    public static EmployeeHistoryDto From(EmployeeHistoryEntry h, bool canSeePay)
        => new(h.Id, h.EffectiveDate, h.Kind, h.Summary, h.IsPay && !canSeePay ? null : h.OldValue, h.IsPay && !canSeePay ? null : h.NewValue, h.IsPay, h.ChangedByUserId);
}
