using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Parties;
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
    /// <summary>Fields shown masked (last characters only), consistent with how parties are displayed.</summary>
    private static readonly HashSet<string> Sensitive = new(StringComparer.OrdinalIgnoreCase) { "Iban", "IdNumber", "TaxNumber", "AccountNumber", "LabourCardNumber" };
    /// <summary>Bookkeeping columns that add noise to a history.</summary>
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase) { "UpdatedAt", "UpdatedBy", "CreatedAt", "CreatedBy", "RowVersion", "OrgId", "IsDeleted", "DeletedAt", "DeletedBy" };

    public async Task<PagedResult<HistoryEntry>> Handle(GetRecordHistory q, CancellationToken ct)
    {
        var spec = new Specification<AuditEvent>(a => a.RecordType == q.RecordType && a.RecordId == q.RecordId);
        var total = await audits.CountAsync(a => a.RecordType == q.RecordType && a.RecordId == q.RecordId, ct);
        var page = await audits.Query(spec).OrderByDescending(a => a.OccurredAt).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        var names = (await directory.ListMembersAsync(ct)).ToDictionary(u => u.Id, u => u.FullName);
        var items = page.Select(a => new HistoryEntry(a.Id, a.OccurredAt, a.UserId, a.UserId is { } u ? names.GetValueOrDefault(u) : null, a.Action, Changes(a))).ToList();
        return new PagedResult<HistoryEntry>(items, total, q.Page, q.PageSize);
    }

    private static List<FieldChange> Changes(AuditEvent a)
    {
        var before = Parse(a.Before);
        var after = Parse(a.After);
        IEnumerable<string> fields = a.Action == "Updated" && !string.IsNullOrEmpty(a.ChangedFields) ? a.ChangedFields.Split(',', StringSplitOptions.RemoveEmptyEntries) : after.Keys.Union(before.Keys);
        return fields.Where(f => !Noise.Contains(f)).Select(f => new FieldChange(f, Show(f, before.GetValueOrDefault(f)), Show(f, after.GetValueOrDefault(f))))
            .Where(c => c.Old != c.New).OrderBy(c => c.Field).ToList();
    }

    private static string? Show(string field, string? value) => Sensitive.Contains(field) ? Masking.Mask(value) : value;

    private static Dictionary<string, string?> Parse(string? json)
    {
        var result = new Dictionary<string, string?>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var p in doc.RootElement.EnumerateObject())
                result[p.Name] = p.Value.ValueKind switch { JsonValueKind.Null => null, JsonValueKind.String => p.Value.GetString(), _ => p.Value.ToString() };
        }
        catch (JsonException) { /* an unreadable snapshot just shows no field changes */ }
        return result;
    }
}
