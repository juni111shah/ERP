using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record PlatformSettingsView(
    SignupMode SignupMode, bool RequireEmailVerification, bool EmailServerConfigured, Guid? DefaultPlanId, string Currency, decimal TaxPercent, string TaxLabel, string InvoicePrefix, int NextInvoiceNumber, int InvoiceDueDays, int RenewalLeadDays, int GraceDays, int SuspendAfterDays,
    string CompanyName, string? CompanyAddress, string? CompanyTaxNumber, string? SupportEmail, string? TermsUrl, string? PaymentInstructions, string TermsText, string PrivacyText, bool LegalIsStarterText, string? AboutText)
{
    internal static PlatformSettingsView From(PlatformSetting s, bool emailServerConfigured) => new(
        s.SignupMode, s.RequireEmailVerification, emailServerConfigured, s.DefaultPlanId, s.Currency, s.TaxPercent, s.TaxLabel, s.InvoicePrefix, s.NextInvoiceNumber, s.InvoiceDueDays, s.RenewalLeadDays, s.GraceDays, s.SuspendAfterDays,
        s.CompanyName, s.CompanyAddress, s.CompanyTaxNumber, s.SupportEmail, s.TermsUrl, s.PaymentInstructions, s.TermsText ?? LegalDefaults.Terms, s.PrivacyText ?? LegalDefaults.Privacy, s.TermsText is null && s.PrivacyText is null, s.AboutText);
}

public sealed record PlatformSettingsInput(
    SignupMode SignupMode, bool RequireEmailVerification, Guid? DefaultPlanId, string Currency, decimal TaxPercent, string TaxLabel, string InvoicePrefix, int InvoiceDueDays, int RenewalLeadDays, int GraceDays, int SuspendAfterDays,
    string? CompanyName, string? CompanyAddress, string? CompanyTaxNumber, string? SupportEmail, string? TermsUrl, string? PaymentInstructions, string? TermsText = null, string? PrivacyText = null, string? AboutText = null);
