using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListExpiringDocumentsHandler(IEmployeeDocumentRepository documents, IEmployeeRepository employees, IClock clock) : IQueryHandler<ListExpiringDocumentsQuery, IReadOnlyList<ExpiringDocumentDto>>
{
    public async Task<IReadOnlyList<ExpiringDocumentDto>> Handle(ListExpiringDocumentsQuery query, CancellationToken ct)
    {
        // 1. Documents expiring by the limit, and their employees still employed.
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var limit = today.AddDays(query.Days);
        var due = await documents.ListExpiringByAsync(limit, ct);
        var ids = due.Select(d => d.EmployeeId).Distinct().ToList();
        var staff = await employees.MapLabelsOfNotTerminatedAsync(ids, ct);

        // 2. Soonest first.
        return due.Where(d => staff.ContainsKey(d.EmployeeId)).OrderBy(d => d.ExpiryDate).ThenBy(d => staff[d.EmployeeId].Number)
            .Select(d => new ExpiringDocumentDto(d.Id, d.EmployeeId, staff[d.EmployeeId].Number, staff[d.EmployeeId].FullName, d.Type, d.Number, d.ExpiryDate!.Value, d.ExpiryDate.Value.DayNumber - today.DayNumber, d.ExpiryDate.Value < today)).ToList();
    }
}
