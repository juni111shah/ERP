using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;

namespace ERP.Application.Finance;

internal sealed class IssueInvoiceHandler(
    IInvoiceRepository invoices,
    IInvoicingService invoicing,
    InvoiceCreditCaps creditCaps,
    ICreditLimitService creditLimits,
    IReadRepository<LegalEntity> legalEntities,
    IReadRepository<Party> parties,
    CountryPackRegistry countryPacks)
    : ICommandHandler<IssueInvoiceCommand, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(IssueInvoiceCommand command, CancellationToken ct)
    {
        // 1. Load it and re-check the credit-note cap (other credit notes may have been issued since it was drafted).
        var invoice = await invoices.GetWithLinesForUpdateAsync(command.Id, ct);
        await creditCaps.EnsureWithinUncreditedAsync(invoice, ct);

        // 2. Going over the customer's credit limit warns; it does not block.
        var creditWarning = !invoice.IsCreditNote && invoice.Status == DocumentStatus.Draft
            ? await creditLimits.CheckAsync(invoice.PartyId, invoice.LegalEntityId, invoice.Total, ct)
            : null;

        // 3. Number, post and save.
        await invoicing.IssueAsync(invoice, ct);
        await invoices.SaveChangesAsync(ct);

        // 4. Answer with the warnings: credit limit first, then what the country pack expects on a tax invoice.
        var warnings = await CountryRequirementWarningsAsync(invoice, ct);
        return InvoiceDto.From(invoice) with { Warnings = creditWarning is null ? warnings : [creditWarning, .. warnings] };
    }

    /// <summary>What the country pack expects on a tax invoice and is missing; informational only.</summary>
    private async Task<IReadOnlyList<string>> CountryRequirementWarningsAsync(Invoice invoice, CancellationToken ct)
    {
        var entity = await legalEntities.FindAsync(invoice.LegalEntityId, ct);
        if (entity is null) return [];
        var customer = await parties.FindAsync(invoice.PartyId, ct);
        return InvoiceRequirements.Warnings(countryPacks.Find(entity.CountryCode), entity, customer, invoice);
    }
}
