namespace ERP.Application.Audit;

public sealed record FieldChange(string Field, string? Old, string? New);
public sealed record HistoryEntry(Guid Id, DateTime OccurredAt, Guid? UserId, string? UserName, string Action, IReadOnlyList<FieldChange> Changes, bool ValuesHidden = false);
