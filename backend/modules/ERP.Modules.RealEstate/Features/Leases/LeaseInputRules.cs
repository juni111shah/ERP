using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Loads what a lease form refers to and copies the form onto the lease (shared by create and update).</summary>
internal sealed class LeaseInputRules(
    IReadRepository<Unit> units, IReadRepository<Property> properties, IReadRepository<Party> parties, IReadRepository<LegalEntity> legalEntities, IReadRepository<TaxCode> taxCodes)
{
    /// <summary>Loads and validates everything a lease refers to (unit, its property, the tenant and the owning legal entity).</summary>
    public async Task<(Unit Unit, Property Property, Party Tenant, LegalEntity Entity)> LoadContextAsync(LeaseInput i, CancellationToken ct)
    {
        var unit = await units.FindAsync(i.UnitId, ct) ?? throw new FieldValidationException("unitId", "Unit not found.");
        var property = await properties.GetAsync(unit.PropertyId, ct);
        var tenant = await parties.FindAsync(i.LesseePartyId, ct) ?? throw new FieldValidationException("lesseePartyId", "Tenant not found.");
        if (tenant.Roles != PartyRoles.None && !tenant.Roles.HasFlag(PartyRoles.Customer)) throw new FieldValidationException("lesseePartyId", "Choose a customer as the tenant.");
        if (i.TaxCodeId is { } taxId && !await taxCodes.AnyAsync(t => t.Id == taxId && t.IsActive, ct)) throw new FieldValidationException("taxCodeId", "Tax code not found.");
        var legalEntity = await legalEntities.GetAsync(property.LegalEntityId, ct);
        return (unit, property, tenant, legalEntity);
    }

    public static void Map(LeaseInput i, Lease lease, Unit unit)
    {
        lease.UnitId = unit.Id; lease.PropertyId = unit.PropertyId; lease.LesseePartyId = i.LesseePartyId;
        lease.StartDate = i.StartDate; lease.EndDate = i.EndDate ?? Lease.DefaultEndDate(i.StartDate); lease.AnnualRent = i.AnnualRent; lease.PaymentFrequency = i.PaymentFrequency;
        lease.SecurityDepositAmount = i.SecurityDepositAmount; lease.TaxCodeId = i.TaxCodeId; lease.GraceDays = i.GraceDays;
        lease.NoticePeriodDays = i.NoticePeriodDays; lease.Notes = i.Notes;
    }
}
