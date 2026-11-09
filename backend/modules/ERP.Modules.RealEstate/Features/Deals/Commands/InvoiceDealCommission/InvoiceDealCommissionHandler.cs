using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class InvoiceDealCommissionHandler(
    IRepository<BrokerageDeal> deals, IInvoicingService invoicing, IPostingService posting, ICustomFieldService fields, IUnitOfWork uow, IClock clock)
    : ICommandHandler<InvoiceDealCommissionCommand, DealDto>
{
    public Task<DealDto> Handle(InvoiceDealCommissionCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The deal needs a client record and a commission.
        var deal = await deals.GetForUpdateAsync(cmd.Id, token);
        if (deal.ClientPartyId is not { } client) throw new ConflictException("Link the client to a customer record before invoicing the commission.");
        if (deal.GrossCommission <= 0) throw new ConflictException("This deal has no commission to invoice.");

        // 2. Raise and issue the invoice.
        var income = await posting.AccountForRoleAsync("realestate.income.brokerage", token);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var description = $"Brokerage commission {deal.Number} ({deal.DealType.ToString().ToLowerInvariant()}{(string.IsNullOrWhiteSpace(deal.UnitReference) ? "" : ", " + deal.UnitReference)})";
        var draft = new InvoiceDraft(deal.LegalEntityId, client, today, today, [new InvoiceLineDraft(description, 1, deal.GrossCommission, deal.TaxCodeId, income)], SourceType: DealCommission.SourceType, SourceId: deal.Id, Notes: deal.Number);
        var invoice = await invoicing.BuildAsync(draft, token);
        await invoicing.IssueAsync(invoice, token);

        // 3. The deal is invoiced; save.
        deal.MarkInvoiced(invoice.Id);
        await deals.SaveChangesAsync(token);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, token));
    }, ct);
}
