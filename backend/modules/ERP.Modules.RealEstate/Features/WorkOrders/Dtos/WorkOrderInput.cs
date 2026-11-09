using System.Text.Json.Nodes;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record WorkOrderInput(
    Guid PropertyId, Guid? UnitId, string Issue, Priority Priority, Guid? LeaseId = null, string? Category = null, DateOnly? ReportedOn = null,
    string? ReportedBy = null, Guid? VendorPartyId = null, string? Technician = null, DateOnly? ScheduledFor = null, decimal EstimatedCost = 0,
    string? Details = null, JsonObject? CustomFields = null);

/// <summary>Who bears the cost of a work order and which vendor bill paid for it. Allowed in any status (the bill usually arrives after the work is done).</summary>
public sealed record CostBasisInput(CostResponsibility? ChargeableTo, decimal? OwnerSharePercent, Guid? BillId);
