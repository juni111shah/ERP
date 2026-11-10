using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>Puts employees on a shift, or (null) takes them off any; shared by the assign and unassign handlers.</summary>
public sealed class ShiftAssignments(IEmployeeRepository employees, ILegalEntityGuard guard)
{
    public async Task<int> SetAsync(IReadOnlyList<Guid> ids, Guid? shiftId, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        var staff = await employees.ListByIdsForUpdateAsync(list, ct);
        if (staff.Count != list.Count) throw new NotFoundException("One of the employees was not found.");
        foreach (var e in staff) { await guard.RequireAsync(e.LegalEntityId, ct); e.ShiftId = shiftId; }
        await employees.SaveChangesAsync(ct);
        return staff.Count;
    }
}
