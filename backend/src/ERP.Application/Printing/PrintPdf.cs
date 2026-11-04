using System.Text;
using ERP.Application.Common;
using ERP.Application.Security;
using MediatR;

namespace ERP.Application.Printing;

/// <summary>The documents that can be downloaded as a PDF. Each one reuses the print data query of the same name.</summary>
public enum PrintDocKind { Invoice, Receipt, Bill, Payment, Journal }

/// <summary>A rendered PDF and the file name it should be saved under.</summary>
public sealed record PrintPdfFile(byte[] Content, string FileName)
{
    public const string ContentType = "application/pdf";
}

/// <summary>
/// Turns print data into a PDF. Implemented in Infrastructure so the application layer does not depend on a PDF library.
/// Everything shown (company details, currency, parties, numbers) comes from the print data; nothing is hardcoded.
/// </summary>
public interface IPrintPdfRenderer
{
    byte[] Render(PrintInvoiceDto doc);
    byte[] Render(PrintReceiptDto doc);
    byte[] Render(PrintBillDto doc);
    byte[] Render(PrintPaymentDto doc);
    byte[] Render(PrintJournalDto doc);
    byte[] Render(PrintGenericDto doc);
}

/// <summary>A print document as a PDF. Same permission as the print data it is built from; an unknown id is a 404.</summary>
public sealed record GetPrintPdf(PrintDocKind Kind, Guid Id) : IQuery<PrintPdfFile>, IRequirePermission
{
    public string RequiredPermission => Kind switch
    {
        PrintDocKind.Invoice => CorePermissions.InvoicesRead,
        PrintDocKind.Receipt => CorePermissions.ReceiptsRead,
        PrintDocKind.Bill => CorePermissions.BillsRead,
        PrintDocKind.Payment => CorePermissions.PaymentsRead,
        PrintDocKind.Journal => CorePermissions.JournalsRead,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };
}

/// <summary>Loads the print data through the existing print queries (so tenant, entity and permission checks are the same) and renders it.</summary>
internal sealed class PrintPdfHandlers(ISender sender, IPrintPdfRenderer renderer) : IRequestHandler<GetPrintPdf, PrintPdfFile>
{
    public async Task<PrintPdfFile> Handle(GetPrintPdf r, CancellationToken ct)
    {
        switch (r.Kind)
        {
            case PrintDocKind.Invoice:
            {
                var d = await sender.Send(new GetPrintInvoice(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName(d.Invoice.IsCreditNote ? "Credit-note" : "Invoice", d.Invoice.Number, r.Id));
            }
            case PrintDocKind.Receipt:
            {
                var d = await sender.Send(new GetPrintReceipt(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName("Receipt", d.Receipt.Number, r.Id));
            }
            case PrintDocKind.Bill:
            {
                var d = await sender.Send(new GetPrintBill(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName("Bill", d.Bill.Number, r.Id));
            }
            case PrintDocKind.Payment:
            {
                var d = await sender.Send(new GetPrintPayment(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName("Payment", d.Payment.Number, r.Id));
            }
            case PrintDocKind.Journal:
            {
                var d = await sender.Send(new GetPrintJournal(r.Id), ct);
                return new(renderer.Render(d), PrintPdfNames.FileName("Journal", d.Journal.Number, r.Id));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(r));
        }
    }
}

public static class PrintPdfNames
{
    /// <summary><c>Invoice-INV-0001.pdf</c>. Only letters, digits, dot, dash and underscore survive so the name is safe in a header and on any disk.</summary>
    public static string FileName(string kind, string? number, Guid id)
    {
        var raw = string.IsNullOrWhiteSpace(number) ? id.ToString("N")[..8] : number.Trim();
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
            sb.Append(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-');
        var safe = sb.ToString().Trim('-', '.');
        if (safe.Length == 0) safe = id.ToString("N")[..8];
        if (safe.Length > 80) safe = safe[..80];
        return $"{kind}-{safe}.pdf";
    }
}
