using ERP.Application.Common;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

public static class VendorRules
{
    /// <summary>A party that has roles set must include Vendor; a party with no roles stays allowed so existing data keeps working.</summary>
    public static void RequireVendor(Party party)
    {
        if (party.Roles != PartyRoles.None && !party.Roles.HasFlag(PartyRoles.Vendor)) throw new FieldValidationException("partyId", "Choose a vendor.");
    }
}
