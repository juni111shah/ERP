using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>The monthly attendance of a posted (or paid) payroll month no longer changes.</summary>
public sealed class AttendancePeriodGuard(IReadRepository<PayrollRun> payrollRuns)
{
    public async Task EnsureNotPostedAsync(Guid entityId, int year, int month, CancellationToken ct)
    {
        if (await payrollRuns.AnyAsync(p => p.LegalEntityId == entityId && p.Year == year && p.Month == month && (p.Status == PayrollStatus.Posted || p.Status == PayrollStatus.Paid), ct))
            throw new ConflictException("The payroll for this month is already posted, so its attendance can no longer change.");
    }
}
