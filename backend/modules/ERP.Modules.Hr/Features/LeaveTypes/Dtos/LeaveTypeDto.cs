using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record LeaveTypeInput(string Code, string Name, bool IsPaid, decimal AnnualDays, decimal CarryForwardCap, bool RequiresApproval = true, bool IsActive = true,
    bool ProRataFirstYear = false, LeaveAccrualMode AccrualMode = LeaveAccrualMode.Upfront, int? RequiresAttachmentAfterDays = null, IReadOnlyList<LeaveTierInput>? Tiers = null, bool Encashable = false, IReadOnlyList<LeavePayBandInput>? PayBands = null);
public sealed record LeaveTypeDto(Guid Id, string Code, string Name, bool IsPaid, decimal AnnualDays, decimal CarryForwardCap, bool RequiresApproval, bool IsActive,
    bool ProRataFirstYear = false, LeaveAccrualMode AccrualMode = LeaveAccrualMode.Upfront, int? RequiresAttachmentAfterDays = null, IReadOnlyList<LeaveTierDto>? Tiers = null, bool Encashable = false, IReadOnlyList<LeavePayBandDto>? PayBands = null)
{
    public static LeaveTypeDto From(LeaveType t, IReadOnlyList<LeaveTierInput>? tiers, IReadOnlyList<LeavePayBandInput>? bands = null) => new(t.Id, t.Code, t.Name, t.IsPaid, t.AnnualDays, t.CarryForwardCap, t.RequiresApproval, t.IsActive,
        t.ProRataFirstYear, t.AccrualMode, t.RequiresAttachmentAfterDays, tiers?.OrderBy(x => x.FromServiceYears).Select(x => new LeaveTierDto(x.FromServiceYears, x.Days)).ToList() ?? [], t.Encashable, bands?.OrderBy(x => x.FromDay).Select(x => new LeavePayBandDto(x.FromDay, x.PayPercent)).ToList() ?? []);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<LeaveType, LeaveTypeDto>> Projection = t => new LeaveTypeDto(t.Id, t.Code, t.Name, t.IsPaid, t.AnnualDays, t.CarryForwardCap, t.RequiresApproval, t.IsActive, t.ProRataFirstYear, t.AccrualMode, t.RequiresAttachmentAfterDays,
        t.Tiers.OrderBy(x => (double)x.FromServiceYears).Select(x => new LeaveTierDto(x.FromServiceYears, x.Days)).ToList(), t.Encashable, t.PayBands.OrderBy(x => x.FromDay).Select(x => new LeavePayBandDto(x.FromDay, x.PayPercent)).ToList());
}
