using System.Text.Json.Nodes;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record PropertyInput(
    Guid LegalEntityId, string Name, PropertyType PropertyType, OwnershipType OwnershipType, Guid? OwnerPartyId, string? Region,
    string? Community, string? PlotNumber, int? YearBuilt, int TotalUnits, decimal IndicativeValue, bool IsActive = true,
    string? Notes = null, JsonObject? CustomFields = null, decimal ManagementFeePercent = 0, bool? CreditRentToOwner = null);
