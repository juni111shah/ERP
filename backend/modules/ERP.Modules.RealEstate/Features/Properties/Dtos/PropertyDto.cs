using System.Text.Json.Nodes;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record PropertyDto(
    Guid Id, string Code, string Name, Guid LegalEntityId, PropertyType PropertyType, OwnershipType OwnershipType, Guid? OwnerPartyId,
    string? Region, string? Community, string? PlotNumber, int? YearBuilt, int TotalUnits, decimal IndicativeValue, bool IsActive,
    string? Notes, JsonObject CustomFields, decimal ManagementFeePercent = 0, bool? CreditRentToOwner = null, bool RentCreditsOwner = false)
{
    public static PropertyDto From(Property p, JsonObject customFields) => new(
        p.Id, p.Code, p.Name, p.LegalEntityId, p.PropertyType, p.OwnershipType, p.OwnerPartyId, p.Region, p.Community, p.PlotNumber,
        p.YearBuilt, p.TotalUnits, p.IndicativeValue, p.IsActive, p.Notes, customFields, p.ManagementFeePercent, p.CreditRentToOwner, p.RentCreditsOwner);
}

/// <summary>Detail view with the KPI cards of spec RE-PROP-03.</summary>
public sealed record PropertySummaryDto(
    PropertyDto Property, int RegisteredUnits, int OccupiedUnits, decimal OccupancyPercent, decimal ContractedAnnualRent,
    decimal GrossYieldPercent, bool UnitsNotFullyRegistered);
