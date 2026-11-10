using ERP.Application.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>An invoice of the console, ready to print, with its client and the platform's company details.</summary>
internal sealed class PlatformInvoiceDocuments(IOrganizationRepository organizations, IWorkspaceSubscriptionRepository subscriptions, PlatformSettingsStore settingsStore)
{
    public async Task<InvoiceDocument> DocumentAsync(SubscriptionInvoice i, CancellationToken ct)
        => InvoiceDocument.From(i, await organizations.GetAsync(i.OrgId, ct), await subscriptions.FindForClientAsync(i.OrgId, ct), await settingsStore.GetAsync(ct));
}
