using ERP.Application.Common;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

public static class CustomerRules
{
    /// <summary>A party that has roles set must include Customer; a party with no roles stays allowed so existing data keeps working.</summary>
    public static void RequireCustomer(Party party)
    {
        if (party.Roles != PartyRoles.None && !party.Roles.HasFlag(PartyRoles.Customer)) throw new FieldValidationException("partyId", "Choose a customer.");
    }
}
