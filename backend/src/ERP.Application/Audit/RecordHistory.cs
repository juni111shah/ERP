using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Audit;

public sealed record FieldChange(string Field, string? Old, string? New);
public sealed record HistoryEntry(Guid Id, DateTime OccurredAt, Guid? UserId, string? UserName, string Action, IReadOnlyList<FieldChange> Changes);

/// <summary>Who changed a record, when, and each field from old to new (from the audit trail). Newest first.</summary>
public sealed record GetRecordHistory(string RecordType, string RecordId, int Page = 1, int PageSize = 25) : IQuery<PagedResult<HistoryEntry>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AuditRead;
}

public sealed class GetRecordHistoryValidator : AbstractValidator<GetRecordHistory>
{
    public GetRecordHistoryValidator()
    {
        RuleFor(x => x.RecordType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RecordId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagedQuery.MaxPageSize);
    }
}

internal sealed class RecordHistoryHandler(IReadRepository<AuditEvent> audits, IUserDirectory directory) : IRequestHandler<GetRecordHistory, PagedResult<HistoryEntry>>
{
    public async Task<PagedResult<HistoryEntry>> Handle(GetRecordHistory q, CancellationToken ct)
    {
        var spec = new Specification<AuditEvent>(a => a.RecordType == q.RecordType && a.RecordId == q.RecordId);
        var total = await audits.CountAsync(a => a.RecordType == q.RecordType && a.RecordId == q.RecordId, ct);
        var page = await audits.Query(spec).OrderByDescending(a => a.OccurredAt).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        var names = (await directory.ListMembersAsync(page.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList(), ct)).ToDictionary(u => u.Id, u => u.FullName);
        var items = page.Select(a => new HistoryEntry(a.Id, a.OccurredAt, a.UserId, a.UserId is { } u ? names.GetValueOrDefault(u) : null, a.Action, AuditSnapshot.Changes(a))).ToList();
        return new PagedResult<HistoryEntry>(items, total, q.Page, q.PageSize);
    }
}
