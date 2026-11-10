using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class UpdatePlatformSettingsHandler(
    PlatformSettingsStore settingsStore, IRepository<PlatformSetting> settings, IReadRepository<SubscriptionPlan> plans, PlatformLog log, IPlatformMailer mailer, IClock clock)
    : ICommandHandler<UpdatePlatformSettingsCommand, PlatformSettingsView>
{
    public async Task<PlatformSettingsView> Handle(UpdatePlatformSettingsCommand command, CancellationToken ct)
    {
        // 1. The default plan must be an active one.
        var i = command.Input;
        if (i.DefaultPlanId is { } planId && !await plans.AnyAsync(p => p.Id == planId && p.IsActive, ct)) throw new FieldValidationException("defaultPlanId", "Choose an active plan.");

        // 2. Copy the input.
        var s = await settingsStore.GetTrackedAsync(ct);
        var wasMode = s.SignupMode;
        s.SignupMode = i.SignupMode; s.RequireEmailVerification = i.RequireEmailVerification; s.DefaultPlanId = i.DefaultPlanId; s.Currency = i.Currency.ToUpperInvariant(); s.TaxPercent = i.TaxPercent; s.TaxLabel = i.TaxLabel.Trim();
        s.InvoicePrefix = i.InvoicePrefix.Trim().ToUpperInvariant(); s.InvoiceDueDays = i.InvoiceDueDays; s.RenewalLeadDays = i.RenewalLeadDays; s.GraceDays = i.GraceDays; s.SuspendAfterDays = i.SuspendAfterDays;
        s.CompanyName = (i.CompanyName ?? "").Trim(); s.CompanyAddress = i.CompanyAddress?.Trim(); s.CompanyTaxNumber = i.CompanyTaxNumber?.Trim(); s.SupportEmail = i.SupportEmail?.Trim(); s.TermsUrl = i.TermsUrl?.Trim(); s.PaymentInstructions = i.PaymentInstructions?.Trim();
        // Sending the starter wording back unchanged leaves it as the starter wording. Sending nothing (older callers) changes nothing.
        var terms = Clean(i.TermsText, LegalDefaults.Terms, s.TermsText);
        var privacy = Clean(i.PrivacyText, LegalDefaults.Privacy, s.PrivacyText);
        if (i.AboutText is not null) s.AboutText = string.IsNullOrWhiteSpace(i.AboutText) ? null : i.AboutText.Trim();
        var legalChanged = terms != s.TermsText || privacy != s.PrivacyText;
        s.TermsText = terms;
        s.PrivacyText = privacy;
        if (legalChanged) s.LegalUpdatedAt = clock.UtcNow;

        // 3. Log and save.
        log.Add("Settings updated", null, null, string.Join(" ", new[] { wasMode == s.SignupMode ? null : $"Registration changed from {wasMode} to {s.SignupMode}.", legalChanged ? "Terms or privacy policy changed." : null }.Where(x => x is not null)) is { Length: > 0 } note ? note : null);
        await settings.SaveChangesAsync(ct);
        return PlatformSettingsView.From(s, mailer.IsEnabled);
    }

    private static string? Clean(string? sent, string starter, string? current)
    {
        if (sent is null) return current;
        var text = sent.Replace("\r\n", "\n").Trim();
        return text.Length == 0 || text == starter.Replace("\r\n", "\n").Trim() ? null : text;
    }
}
