using System.Text.Json.Nodes;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record WorkOrderDto(
    Guid Id, string? Number, Guid PropertyId, Guid? UnitId, Guid? LeaseId, Guid LegalEntityId, string Issue, string? Category, Priority Priority,
    WorkOrderStatus Status, DateOnly ReportedOn, string? ReportedBy, Guid? VendorPartyId, string? Technician, DateOnly? ScheduledFor, DateOnly? CompletedOn,
    decimal EstimatedCost, decimal? ActualCost, string? Details, string? ResolutionNotes, JsonObject CustomFields,
    CostResponsibility? ChargeableTo = null, decimal OwnerSharePercent = 100, Guid? BillId = null)
{
    public static WorkOrderDto From(WorkOrder w, JsonObject customFields) => new(
        w.Id, w.Number, w.PropertyId, w.UnitId, w.LeaseId, w.LegalEntityId, w.Issue, w.Category, w.Priority, w.Status, w.ReportedOn, w.ReportedBy,
        w.VendorPartyId, w.Technician, w.ScheduledFor, w.CompletedOn, w.EstimatedCost, w.ActualCost, w.Details, w.ResolutionNotes, customFields, w.ChargeableTo, w.OwnerSharePercent, w.BillId);
}
