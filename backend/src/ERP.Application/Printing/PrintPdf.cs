using System.Text;

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
