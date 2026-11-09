using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Checks what a schedule form refers to and copies it onto the schedule (shared by create and update).</summary>
internal sealed class PmScheduleInputRules(IReadRepository<Property> properties, IReadRepository<Domain.Unit> units)
{
    public async Task MapAsync(PmScheduleInput i, PmSchedule s, CancellationToken ct)
    {
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && u.PropertyId == i.PropertyId, ct)) throw new FieldValidationException("unitId", "That unit is not in this property.");
        s.LegalEntityId = property.LegalEntityId;
        s.PropertyId = i.PropertyId; s.UnitId = i.UnitId; s.Title = i.Title.Trim(); s.Category = i.Category; s.Priority = i.Priority; s.FrequencyMonths = i.FrequencyMonths;
        s.NextDue = i.NextDue; s.LeadDays = i.LeadDays; s.EstimatedCost = i.EstimatedCost; s.IsActive = i.IsActive; s.Notes = i.Notes;
    }
}
