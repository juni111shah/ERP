using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Checks the owner of a property form and copies the form onto the entity (shared by create and update).</summary>
internal sealed class PropertyInputRules(IReadRepository<Party> parties)
{
    public async Task EnsureOwnerAsync(PropertyInput i, CancellationToken ct)
    {
        if (i.OwnershipType != OwnershipType.ManagedForOwner) return;
        if (i.OwnerPartyId is not { } ownerId || !await parties.AnyAsync(p => p.Id == ownerId, ct))
            throw new FieldValidationException("ownerPartyId", "Owner not found.");
    }

    public static void Map(PropertyInput i, Property p)
    {
        p.Name = i.Name.Trim(); p.PropertyType = i.PropertyType; p.OwnershipType = i.OwnershipType;
        p.OwnerPartyId = i.OwnershipType == OwnershipType.ManagedForOwner ? i.OwnerPartyId : null;
        p.Region = i.Region; p.Community = i.Community; p.PlotNumber = i.PlotNumber; p.YearBuilt = i.YearBuilt;
        p.ManagementFeePercent = i.OwnershipType == OwnershipType.ManagedForOwner ? i.ManagementFeePercent : 0;
        p.CreditRentToOwner = i.OwnershipType == OwnershipType.ManagedForOwner ? i.CreditRentToOwner ?? p.CreditRentToOwner : null; // a form that does not send it keeps the choice
        p.TotalUnits = i.TotalUnits; p.IndicativeValue = i.IndicativeValue; p.IsActive = i.IsActive; p.Notes = i.Notes;
    }
}
