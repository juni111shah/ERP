using ERP.Domain.Common;

namespace ERP.Domain.Platform;

/// <summary>Immutable audit trail written by the SaveChanges interceptor.</summary>
[DefaultSort("-occurredAt")]
public class AuditEvent : Entity, IOrgScoped
{
    public Guid OrgId { get; set; }
    [Sortable, Filterable]
    public DateTime OccurredAt { get; set; }
    [Filterable]
    public Guid? UserId { get; set; }
    [Filterable]
    public string Action { get; set; } = default!; // Created | Updated | Deleted | custom
    [Filterable, Searchable]
    public string RecordType { get; set; } = default!;
    [Filterable, Searchable]
    public string RecordId { get; set; } = default!;
    public string? ChangedFields { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
}

public class Notification : OrgEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = default!;
    public string? Body { get; set; }
    public string? Link { get; set; }
    public string Severity { get; set; } = "info";
    public DateTime? ReadAt { get; set; }
}

/// <summary>File attached to any record (owner type + id). Content lives in an IFileStore.</summary>
public class Attachment : OrgEntity
{
    public string OwnerType { get; set; } = default!;
    public Guid OwnerId { get; set; }
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = default!;
}

/// <summary>Key/value settings (JSON values). LegalEntityId null = tenant-wide. Modules namespace their keys ("<moduleKey>.*").</summary>
public class Setting : OrgEntity
{
    public Guid? LegalEntityId { get; set; }
    public string Key { get; set; } = default!;
    public string Value { get; set; } = "null";
}

/// <summary>Document numbering (INV-2026-000001). Optionally per legal entity, reset per year.</summary>
public class NumberSequence : OrgEntity
{
    public Guid? LegalEntityId { get; set; }
    public string DocumentType { get; set; } = default!;
    public string Prefix { get; set; } = default!;
    /// <summary>Tokens: {prefix} {yyyy} {yy} {seq:N}.</summary>
    public string Pattern { get; set; } = "{prefix}-{yyyy}-{seq:6}";
    public bool ResetYearly { get; set; } = true;
    public int Year { get; set; }
    public long NextValue { get; set; } = 1;
}

public enum ApprovalStatus { Pending, Approved, Rejected, Withdrawn }

/// <summary>When a document type needs approval (maker-checker). Amount threshold optional.</summary>
public class ApprovalPolicy : OrgEntity
{
    public string DocumentType { get; set; } = default!;
    public decimal? MinAmount { get; set; }
    public string ApproverPermission { get; set; } = "approvals.decide";
    public bool IsActive { get; set; } = true;
}

public class ApprovalRequest : OrgEntity
{
    public Guid? LegalEntityId { get; set; }
    public string DocumentType { get; set; } = default!;
    public Guid? DocumentId { get; set; }
    public string Subject { get; set; } = default!;
    public string? Justification { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }

    /// <summary>Segregation of duties: the requester can never decide their own request.</summary>
    public void Decide(Guid deciderId, bool approve, string? comment, DateTime now)
    {
        if (Status != ApprovalStatus.Pending) throw new DomainException("This request has already been decided.");
        if (deciderId == RequestedBy) throw new DomainException("Approvers can never approve their own requests.");
        Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        DecidedBy = deciderId;
        DecidedAt = now;
        DecisionComment = comment;
    }

    public void Withdraw(Guid userId)
    {
        if (Status != ApprovalStatus.Pending) throw new DomainException("Only pending requests can be withdrawn.");
        if (userId != RequestedBy) throw new DomainException("Only the requester can withdraw a request.");
        Status = ApprovalStatus.Withdrawn;
    }
}

public enum MessageStatus { Sent = 1, Failed = 2 }

/// <summary>Communication log: every reminder/message the system sent (or failed to send), one row per recipient. DedupeKey makes sending idempotent.</summary>
[DefaultSort("-sentAt")]
public class MessageLog : OrgEntity
{
    [Filterable]
    public string Channel { get; set; } = default!; // inapp | email | ...
    [Searchable]
    public string Recipient { get; set; } = default!; // user id or address
    [Searchable]
    public string Subject { get; set; } = default!;
    public string? Body { get; set; }
    public string? Link { get; set; }
    public string DedupeKey { get; set; } = default!;
    [Filterable]
    public MessageStatus Status { get; set; }
    public string? Error { get; set; }
    [Sortable, Filterable]
    public DateTime SentAt { get; set; }
}
