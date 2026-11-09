using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record VendorWorkOrderDto(Guid Id, string? Number, string Issue, string? Category, Priority Priority, WorkOrderStatus Status, DateOnly ReportedOn, DateOnly? ScheduledFor, DateOnly? CompletedOn, decimal EstimatedCost, decimal? ActualCost, string? ResolutionNotes)
{
    public static VendorWorkOrderDto From(WorkOrder w) => new(w.Id, w.Number, w.Issue, w.Category, w.Priority, w.Status, w.ReportedOn, w.ScheduledFor, w.CompletedOn, w.EstimatedCost, w.ActualCost, w.ResolutionNotes);
}

public sealed record VendorWorkOrderUpdate(WorkOrderStatus Status, decimal? ActualCost = null, string? Notes = null);
