using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetLegalDocumentsHandler(PlatformSettingsStore settingsStore) : IQueryHandler<GetLegalDocumentsQuery, LegalDocuments>
{
    public async Task<LegalDocuments> Handle(GetLegalDocumentsQuery query, CancellationToken ct)
    {
        var s = await settingsStore.GetAsync(ct);
        return new(s.CompanyName, s.SupportEmail, s.TermsUrl,
            LegalDefaults.Fill(s.TermsText ?? LegalDefaults.Terms, s.CompanyName, s.SupportEmail),
            LegalDefaults.Fill(s.PrivacyText ?? LegalDefaults.Privacy, s.CompanyName, s.SupportEmail), s.LegalUpdatedAt ?? s.CreatedAt);
    }
}
