using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record BrandLeadInput(string? Token, string Name, string Email, string? Phone, string? Company, string? Need, bool Consent, string? Website);

public sealed record BrandLeadRow(Guid Id, DateTime CreatedAt, BrandLeadStatus Status, string Name, string Email, string? Phone, string? Company, string? Need, Guid? ConversationId, bool HasRegistered)
{
    internal static BrandLeadRow From(BrandLead l, bool registered) => new(l.Id, l.CreatedAt, l.Status, l.Name, l.Email, l.Phone, l.Company, l.Need, l.ConversationId, registered);
}
