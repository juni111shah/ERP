using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Parties;

namespace ERP.Application.Printing;

internal sealed class GetPrintBillHandler(IBillRepository bills, IReadRepository<Party> parties, PrintLookups lookups) : IQueryHandler<GetPrintBillQuery, PrintBillDto>
{
    public async Task<PrintBillDto> Handle(GetPrintBillQuery query, CancellationToken ct)
    {
        // 1. The bill, the company header and the vendor.
        var bill = await bills.GetWithLinesAsync(query.Id, ct);
        var header = await lookups.BuildHeaderAsync(bill.LegalEntityId, ct);
        var party = await parties.FindAsync(bill.PartyId, ct);

        // 2. Lines in order.
        var lines = bill.Lines.OrderBy(l => l.Seq).Select(l => new BillLineDto(
            l.Id, l.Seq, l.Description, l.Quantity, l.Price, l.TaxCodeId, l.TaxAmount, l.TaxRecoverable, l.Net, l.AccountId)).ToList();

        var billDto = new BillDto(
            bill.Id, bill.Number, bill.LegalEntityId, bill.PartyId, bill.PartyName, bill.VendorReference,
            bill.BillDate, bill.DueDate, bill.CurrencyCode, bill.Net, bill.Tax, bill.Total, bill.Paid,
            bill.Status, bill.ApprovalRequestId, bill.JournalEntryId, bill.Notes, lines);

        return new PrintBillDto(
            header,
            billDto,
            party?.Name ?? bill.PartyName ?? "Unknown",
            party?.Address,
            lines, party?.TaxNumber, party?.Phone, party?.Email);
    }
}
