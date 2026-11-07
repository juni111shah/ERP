using ERP.Application.Abstractions;

namespace ERP.Application.Audit;

/// <summary>
/// Declares that the field values of a record type's audit events need a permission beyond <c>core.audit.read</c>, because the
/// trail copies every column of a row. A module registers one per sensitive record type. Without the permission the caller still
/// sees who changed the record, when, and which fields, but no values.
/// </summary>
public interface IAuditFieldPolicy
{
    /// <summary>The record type as stored on the audit event (the entity's class name).</summary>
    string RecordType { get; }
    string RequiredPermission { get; }
}

public sealed record AuditFieldPolicy(string RecordType, string RequiredPermission) : IAuditFieldPolicy;

/// <summary>Answers, for the current caller, whether an audit event's field values may be shown.</summary>
internal sealed class AuditFieldAccess(IEnumerable<IAuditFieldPolicy> policies, ICurrentUser user)
{
    /// <summary>
    /// No longer written to the trail, but older events may still exist and hold secrets, so nobody reads their values.
    /// Generic classes are stored under their runtime name, hence the suffix.
    /// </summary>
    private static readonly HashSet<string> NeverShown = new(StringComparer.Ordinal) { "IdentityUserToken`1", "IdentityUserClaim`1", "IdentityUserLogin`1", "IdempotencyRecord" };

    private readonly ILookup<string, string> _required = policies.ToLookup(p => p.RecordType, p => p.RequiredPermission, StringComparer.Ordinal);

    public bool CanReadValues(string recordType) => !NeverShown.Contains(recordType) && _required[recordType].All(user.HasPermission);
}
