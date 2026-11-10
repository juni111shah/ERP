using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Copies a leave type input onto a leave type (create and update).</summary>
internal static class LeaveTypeMapping
{
    public static void Map(LeaveTypeInput i, LeaveType t, string code)
    {
        t.Code = code; t.Name = i.Name.Trim(); t.IsPaid = i.IsPaid; t.AnnualDays = i.AnnualDays; t.CarryForwardCap = i.CarryForwardCap; t.RequiresApproval = i.RequiresApproval; t.IsActive = i.IsActive;
        t.ProRataFirstYear = i.ProRataFirstYear; t.AccrualMode = i.AccrualMode; t.RequiresAttachmentAfterDays = i.RequiresAttachmentAfterDays; t.Encashable = i.Encashable;
    }
}
