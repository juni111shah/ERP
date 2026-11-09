using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

/// <summary>Shared by the party handlers: copies the input onto a party, checks its group and shapes the answer (sensitive values masked).</summary>
public sealed class PartyMapping(IReadRepository<PartyGroup> groups, ICurrentUser user)
{
    /// <summary>The custom-field entity type of parties.</summary>
    public const string EntityType = "party";

    public static void Apply(PartyInput i, Party p)
    {
        p.Name = i.Name.Trim(); p.Kind = i.Kind; p.Roles = i.Roles; p.Email = i.Email; p.Phone = i.Phone; p.TaxNumber = i.TaxNumber;
        p.IdNumber = i.IdNumber; p.Iban = i.Iban; p.CurrencyCode = string.IsNullOrWhiteSpace(i.CurrencyCode) ? null : i.CurrencyCode.Trim().ToUpperInvariant(); p.PaymentTermsDays = i.PaymentTermsDays;
        p.Address = i.Address; p.IsActive = i.IsActive; p.PartyGroupId = i.PartyGroupId;
        // Left out means unchanged, so a client that does not know the field cannot drop a limit by saving the party.
        p.CreditLimit = i.ClearCreditLimit ? null : i.CreditLimit ?? p.CreditLimit;
    }

    public async Task EnsureGroupAsync(Guid? groupId, CancellationToken ct)
    {
        if (groupId is { } id && !await groups.AnyAsync(g => g.Id == id, ct)) throw new FieldValidationException("partyGroupId", "Party group not found.");
    }

    public PartyDto ToDto(Party p, JsonObject cf)
    {
        var sensitive = user.HasPermission(CorePermissions.SensitiveRead);
        var roles = Enum.GetValues<PartyRoles>().Where(r => r != PartyRoles.None && p.Roles.HasFlag(r)).Select(r => r.ToString()).ToArray();
        return new(p.Id, p.Code, p.Name, p.Kind.ToString(), roles, p.Email, p.Phone, p.TaxNumber,
            sensitive ? p.IdNumber : Masking.Mask(p.IdNumber), sensitive ? p.Iban : Masking.Mask(p.Iban),
            p.CurrencyCode, p.PaymentTermsDays, p.Address, p.IsActive, cf, p.PartyGroupId, p.CreditLimit);
    }
}
