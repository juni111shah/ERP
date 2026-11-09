using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Parties;
using ERP.Domain.Parties;

namespace ERP.Modules.Pos.Features;

/// <summary>Who is on a ticket: the cashier (always the signed-in caller) and the customer. Used by sale, quote and hold.</summary>
public sealed class PosOrderParties(ICurrentUser currentUser, IUserDirectory users, IReadRepository<Party> parties)
{
    public async Task<(string Name, Guid Id)> CashierAsync(Guid? requestedId, string? requestedName, CancellationToken ct)
    {
        // Cashier identity from ICurrentUser; client-supplied cashier is ignored
        var cashierId = (currentUser.UserId.HasValue && currentUser.UserId.Value != Guid.Empty)
            ? currentUser.UserId.Value
            : (requestedId ?? Guid.Empty);
        var profile = cashierId != Guid.Empty ? await users.GetProfileAsync(cashierId, ct) : null;
        var name = !string.IsNullOrWhiteSpace(profile?.FullName)
            ? profile!.FullName
            : !string.IsNullOrWhiteSpace(currentUser.Email)
            ? currentUser.Email!
            : (currentUser.UserId.HasValue ? currentUser.UserId.Value.ToString() : (!string.IsNullOrWhiteSpace(requestedName) ? requestedName.Trim() : "POS Cashier"));
        return (name, cashierId);
    }

    /// <summary>A chosen customer must really be one, and the name on the ticket then comes from the customer record.</summary>
    public async Task<(Guid? Id, string Name)> CustomerAsync(Guid? partyId, string? typedName, CancellationToken ct)
    {
        if (partyId is null) return (null, string.IsNullOrWhiteSpace(typedName) ? "Walk-in Customer" : typedName.Trim());
        var party = await parties.FindAsync(partyId.Value, ct) ?? throw new FieldValidationException("customerPartyId", "Customer not found.");
        CustomerRules.RequireCustomer(party);
        return (party.Id, party.Name);
    }
}
