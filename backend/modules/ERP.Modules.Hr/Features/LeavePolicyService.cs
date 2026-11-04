using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record LeaveTierInput(decimal FromServiceYears, decimal Days);
public sealed record LeaveTierDto(decimal FromServiceYears, decimal Days);

/// <summary>Database-backed parts of the leave policy: tiers, per-year deduction rows and the supporting-document rule. The arithmetic itself is in <see cref="LeaveEntitlement"/>.</summary>
public sealed class LeavePolicyService(IRepository<LeaveEntitlementTier> tiers, IRepository<LeaveRequestYearDays> yearDays, IReadRepository<Attachment> attachments, IClock clock)
{
    public const string AttachmentOwnerType = "hr.leaverequest";

    public DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<List<(decimal FromServiceYears, decimal Days)>> TiersAsync(Guid leaveTypeId, CancellationToken ct)
        => (await tiers.Query(new Specification<LeaveEntitlementTier>(t => t.LeaveTypeId == leaveTypeId)).ToListAsync(ct)).Select(t => (t.FromServiceYears, t.Days)).ToList();

    public async Task<Dictionary<Guid, List<(decimal FromServiceYears, decimal Days)>>> TiersByTypeAsync(CancellationToken ct)
        => (await tiers.Query().ToListAsync(ct)).GroupBy(t => t.LeaveTypeId).ToDictionary(g => g.Key, g => g.Select(t => (t.FromServiceYears, t.Days)).ToList());

    public async Task<decimal> EntitlementAsync(Employee e, LeaveType type, int year, CancellationToken ct)
        => LeaveEntitlement.Entitlement(type.AnnualDays, await TiersAsync(type.Id, ct), type.ProRataFirstYear, e.JoinDate, year);

    /// <summary>Replaces the type's tiers with the given list (null = leave them alone).</summary>
    public async Task SaveTiersAsync(Guid leaveTypeId, IReadOnlyList<LeaveTierInput>? input, CancellationToken ct)
    {
        if (input is null) return;
        foreach (var old in await tiers.Tracked(new Specification<LeaveEntitlementTier>(t => t.LeaveTypeId == leaveTypeId)).ToListAsync(ct)) tiers.Remove(old);
        foreach (var t in input.GroupBy(x => x.FromServiceYears).Select(g => g.Last()))
            tiers.Add(new LeaveEntitlementTier { LeaveTypeId = leaveTypeId, FromServiceYears = t.FromServiceYears, Days = t.Days });
    }

    /// <summary>Per-year deduction of an approved request; requests approved before year splitting existed have no rows and count wholly in their start year.</summary>
    public async Task<Dictionary<int, decimal>> StoredYearDaysAsync(LeaveRequest r, CancellationToken ct)
    {
        var id = r.Id;
        var rows = await yearDays.Query(new Specification<LeaveRequestYearDays>(x => x.RequestId == id)).ToListAsync(ct);
        return rows.Count == 0 ? new() { [r.FromDate.Year] = r.Days } : rows.ToDictionary(x => x.Year, x => x.Days);
    }

    public void StoreYearDays(LeaveRequest r, Dictionary<int, decimal> split)
    {
        foreach (var (year, days) in split) yearDays.Add(new LeaveRequestYearDays { RequestId = r.Id, Year = year, Days = days });
    }

    /// <summary>Throws (409) when the type asks for a supporting document for a request this long and none is attached.</summary>
    public async Task RequireAttachmentAsync(LeaveRequest r, LeaveType type, CancellationToken ct)
    {
        if (type.RequiresAttachmentAfterDays is not { } limit || r.Days <= limit) return;
        var id = r.Id;
        if (!await attachments.AnyAsync(a => a.OwnerType == AttachmentOwnerType && a.OwnerId == id, ct))
            throw new ConflictException("Attach a medical certificate before submitting.");
    }
}
