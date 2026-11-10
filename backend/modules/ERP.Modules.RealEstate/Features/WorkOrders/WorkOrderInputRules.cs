using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Checks what a work order form refers to and copies it onto the work order (shared by create and update).</summary>
internal sealed class WorkOrderInputRules(IReadRepository<Unit> units, IReadRepository<Lease> leases, IReadRepository<Party> parties)
{
    public async Task MapAsync(WorkOrderInput i, WorkOrder w, CancellationToken ct)
    {
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && u.PropertyId == i.PropertyId, ct))
            throw new FieldValidationException("unitId", "That unit does not belong to the property.");
        if (i.LeaseId is { } leaseId && !await leases.AnyAsync(l => l.Id == leaseId && l.PropertyId == i.PropertyId, ct))
            throw new FieldValidationException("leaseId", "That lease does not belong to the property.");
        if (i.VendorPartyId is { } vendorId && !await parties.AnyAsync(p => p.Id == vendorId, ct))
            throw new FieldValidationException("vendorPartyId", "Vendor not found.");
        w.PropertyId = i.PropertyId; w.UnitId = i.UnitId; w.LeaseId = i.LeaseId; w.Issue = i.Issue.Trim(); w.Category = i.Category; w.Priority = i.Priority;
        if (i.ReportedOn is { } reported) w.ReportedOn = reported;
        w.ReportedBy = i.ReportedBy; w.VendorPartyId = i.VendorPartyId; w.Technician = i.Technician; w.ScheduledFor = i.ScheduledFor;
        w.EstimatedCost = i.EstimatedCost; w.Details = i.Details;
    }
}
