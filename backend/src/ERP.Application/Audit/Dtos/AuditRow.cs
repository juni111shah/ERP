namespace ERP.Application.Audit;

/// <summary>
/// One row of the audit log. It carries no field values, so listing never exposes more than the record's name and which fields moved.
/// <c>ValuesHidden</c> is true when the caller may not read this record type's values (see <see cref="IAuditFieldPolicy"/>); the name is then left out too.
/// </summary>
public sealed record AuditRow(Guid Id, DateTime OccurredAt, Guid? UserId, string? UserName, string Action, string RecordType, string RecordId, string? RecordLabel, string? RecordKind, IReadOnlyList<string> ChangedFields, string? IpAddress, bool ValuesHidden = false);

/// <summary>A field of the record at the time of the event. <c>Changed</c> is false for values the event left alone.</summary>
public sealed record AuditFieldDiff(string Field, string? Old, string? New, bool Changed);

public sealed record AuditEventDetail(AuditRow Event, IReadOnlyList<AuditFieldDiff> Fields, string? Before, string? After, IReadOnlyList<AuditRow> SameSave, int RecordEventCount);

public sealed record AuditUserOption(Guid Id, string Name);

public sealed record AuditFacets(IReadOnlyList<string> RecordTypes, IReadOnlyList<string> Actions, IReadOnlyList<AuditUserOption> Users, int Today, int Last7Days, int DeletionsLast7Days, int ActiveUsersLast7Days);
