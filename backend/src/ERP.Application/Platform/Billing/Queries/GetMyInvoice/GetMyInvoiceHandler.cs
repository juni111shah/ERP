using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;

namespace ERP.Application.Platform;

internal sealed class GetMyInvoiceHandler(
    ISubscriptionInvoiceRepository invoices, IReadRepository<Organization> organizations, IWorkspaceSubscriptionRepository subscriptions, PlatformSettingsStore settingsStore, ICurrentUser user)
    : IQueryHandler<GetMyInvoiceQuery, InvoiceDocument>
{
    public async Task<InvoiceDocument> Handle(GetMyInvoiceQuery query, CancellationToken ct)
    {
        var orgId = user.OrgId ?? throw new ForbiddenException();
        var invoice = await invoices.FindForClientAsync(query.Id, orgId, ct) ?? throw new NotFoundException();
        return InvoiceDocument.From(invoice, await organizations.GetAsync(orgId, ct), await subscriptions.FindForClientAsync(orgId, ct), await settingsStore.GetAsync(ct));
    }
}
