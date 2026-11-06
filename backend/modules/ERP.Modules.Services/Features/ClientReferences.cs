using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;

namespace ERP.Modules.Services.Features;

/// <summary>Checks a client reference. Documents always show the name on the customer record, not one typed in a request.</summary>
public sealed class ClientReferences(IReadRepository<Party> parties)
{
    public async Task<string> RequireNameAsync(Guid clientId, CancellationToken ct)
    {
        var party = await parties.FindAsync(clientId, ct) ?? throw new FieldValidationException("clientId", "Client not found.");
        if (party.Roles != PartyRoles.None && !party.Roles.HasFlag(PartyRoles.Customer)) throw new FieldValidationException("clientId", "Choose a customer as the client.");
        return party.Name;
    }
}
