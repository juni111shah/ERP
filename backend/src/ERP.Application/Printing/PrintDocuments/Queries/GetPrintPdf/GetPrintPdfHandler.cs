using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Printing;

/// <summary>Loads the print data through the existing print queries (so tenant, entity and permission checks are the same) and renders it.</summary>
internal sealed class GetPrintPdfHandler(ISender sender, IPrintPdfRenderer renderer) : IQueryHandler<GetPrintPdfQuery, PrintPdfFile>
{
    public async Task<PrintPdfFile> Handle(GetPrintPdfQuery r, CancellationToken ct)
    {
        switch (r.Kind)
        {
            case PrintDocKind.Invoice:
            {
                var d = await sender.Send(new GetPrintInvoiceQuery(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName(d.Invoice.IsCreditNote ? "Credit-note" : "Invoice", d.Invoice.Number, r.Id));
            }
            case PrintDocKind.Receipt:
            {
                var d = await sender.Send(new GetPrintReceiptQuery(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName("Receipt", d.Receipt.Number, r.Id));
            }
            case PrintDocKind.Bill:
            {
                var d = await sender.Send(new GetPrintBillQuery(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName("Bill", d.Bill.Number, r.Id));
            }
            case PrintDocKind.Payment:
            {
                var d = await sender.Send(new GetPrintPaymentQuery(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName("Payment", d.Payment.Number, r.Id));
            }
            case PrintDocKind.Journal:
            {
                var d = await sender.Send(new GetPrintJournalQuery(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName("Journal", d.Journal.Number, r.Id));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(r));
        }
    }
}
