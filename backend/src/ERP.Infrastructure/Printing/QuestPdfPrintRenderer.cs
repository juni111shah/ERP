using System.Globalization;
using System.Text.RegularExpressions;
using ERP.Application.Printing;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Printing;

/// <summary>
/// Server-side PDF for the print documents, built with QuestPDF (Community licence, see the PR / docs for the terms).
/// The layout mirrors the web print page: company block, document title and number, party, meta, lines, totals, notes.
/// Every name, address, currency and number comes from the print data; labels are generic (no country or industry wording).
/// Fonts: Lato (bundled with QuestPDF; Latin, Greek, Cyrillic) with Noto Sans Arabic (SIL OFL 1.1, embedded) as fallback,
/// so Arabic names and addresses render shaped and right-to-left. A character neither font has never fails the document.
/// </summary>
public sealed class QuestPdfPrintRenderer : IPrintPdfRenderer
{
    private const string ArabicFamily = "Noto Sans Arabic";
    private static readonly string[] Fonts = ["Lato", ArabicFamily];
    private static readonly string[] FontResources = ["ERP.Infrastructure.Printing.Fonts.NotoSansArabic-Regular.ttf", "ERP.Infrastructure.Printing.Fonts.NotoSansArabic-Bold.ttf"];

    private const string Ink = "#0F172A";
    private const string Muted = "#64748B";
    private const string Rule = "#E2E8F0";
    private const string HeadFill = "#F1F5F9";
    private const string Accent = "#0F766E";
    private const string Danger = "#B91C1C";
    private const string Warn = "#B45309";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly Lazy<bool> Setup = new(Configure, LazyThreadSafetyMode.ExecutionAndPublication);

    private static bool Configure()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false; // same output on every server
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false; // an unusual script falls back to a placeholder instead of failing
        var asm = typeof(QuestPdfPrintRenderer).Assembly;
        foreach (var name in FontResources)
        {
            using var stream = asm.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Embedded font {name} is missing.");
            FontManager.RegisterFontFromStream(stream);
        }
        return true;
    }

    public QuestPdfPrintRenderer() => _ = Setup.Value;

    // ----- documents -----

    public byte[] Render(PrintInvoiceDto d)
    {
        var i = d.Invoice;
        var title = i.IsCreditNote ? "Credit note" : string.IsNullOrWhiteSpace(d.Header.TaxRegistrationNo) ? "Invoice" : "Tax invoice";
        var noLabel = i.IsCreditNote ? "Credit note no." : "Invoice no.";
        var meta = new List<(string, string)> { (noLabel, i.Number), ("Date", Date(i.InvoiceDate)), ("Due date", Date(i.DueDate)) };
        if (i.IsCreditNote && !string.IsNullOrWhiteSpace(d.OriginalInvoiceNumber)) meta.Add(("Original invoice", d.OriginalInvoiceNumber!));
        meta.Add(("Currency", i.CurrencyCode));
        var terms = new List<string>();
        if (!i.IsCreditNote) terms.Add("Please quote the invoice number with your payment. Thank you for your business.");
        return Compose(new Doc(
            d.Header, title, i.Number, Stamp(i.Status.ToString()),
            i.IsCreditNote ? "Credit to" : "Bill to", i.PartyName, PartyLines(d.PartyAddress, d.PartyTaxNumber, d.PartyPhone, d.PartyEmail),
            meta, LineColumns, [.. d.Lines.OrderBy(l => l.Seq).Select((l, n) => LineRow(n + 1, l.Description, l.Quantity, l.Price, l.Net, l.TaxAmount))],
            DocTotals(i.Net, i.Tax, i.Total, i.Paid, i.CurrencyCode), i.Notes, terms));
    }

    public byte[] Render(PrintReceiptDto d)
    {
        var r = d.Receipt;
        var meta = new List<(string, string)> { ("Receipt no.", r.Number), ("Date", Date(r.Date)), ("Method", Humanize(r.Method.ToString())) };
        if (!string.IsNullOrWhiteSpace(r.Reference)) meta.Add(("Reference", r.Reference!));
        AddInstrument(meta, d.InstrumentKind, d.ChequeNumber, d.ChequeDate, d.ChequeBankName, d.ChequeStatus);
        meta.Add(("Currency", r.CurrencyCode));
        var rows = d.Allocations.Select((a, n) => new[] { (n + 1).ToString(Inv), Coalesce(a.InvoiceNumber, a.Label), Money(a.Amount) }).ToList();
        var totals = new List<TotalLine>();
        if (r.Unallocated != 0) totals.Add(new("Unallocated", Money(r.Unallocated)));
        totals.Add(new($"Amount received ({r.CurrencyCode})", Money(r.Amount), true));
        return Compose(new Doc(d.Header, "Payment receipt", r.Number, Stamp(r.Status.ToString()),
            "Received from", d.PartyName, PartyLines(d.PartyAddress, d.PartyTaxNumber, d.PartyPhone, d.PartyEmail),
            meta, AllocationColumns("Invoice no."), rows, totals, null, ["Thank you for your payment."]));
    }

    public byte[] Render(PrintBillDto d)
    {
        var b = d.Bill;
        var meta = new List<(string, string)> { ("Bill no.", b.Number) };
        if (!string.IsNullOrWhiteSpace(b.VendorReference)) meta.Add(("Vendor ref.", b.VendorReference!));
        meta.Add(("Date", Date(b.BillDate)));
        meta.Add(("Due date", Date(b.DueDate)));
        meta.Add(("Currency", b.CurrencyCode));
        return Compose(new Doc(d.Header, "Purchase bill", b.Number, Stamp(b.Status.ToString()),
            "Vendor", d.PartyName, PartyLines(d.PartyAddress, d.PartyTaxNumber, d.PartyPhone, d.PartyEmail),
            meta, LineColumns, [.. d.Lines.OrderBy(l => l.Seq).Select((l, n) => LineRow(n + 1, l.Description, l.Quantity, l.Price, l.Net, l.TaxAmount))],
            DocTotals(b.Net, b.Tax, b.Total, b.Paid, b.CurrencyCode), b.Notes, []));
    }

    public byte[] Render(PrintPaymentDto d)
    {
        var p = d.Payment;
        var meta = new List<(string, string)> { ("Voucher no.", p.Number), ("Date", Date(p.Date)), ("Method", Humanize(p.Method.ToString())) };
        if (!string.IsNullOrWhiteSpace(p.Reference)) meta.Add(("Reference", p.Reference!));
        AddInstrument(meta, d.InstrumentKind, d.ChequeNumber, d.ChequeDate, d.ChequeBankName, d.ChequeStatus);
        meta.Add(("Currency", p.CurrencyCode));
        var rows = d.Allocations.Select((a, n) => new[] { (n + 1).ToString(Inv), Coalesce(a.BillNumber, a.Label), Money(a.Amount) }).ToList();
        var totals = new List<TotalLine>();
        if (p.Unallocated != 0) totals.Add(new("Unallocated", Money(p.Unallocated)));
        totals.Add(new($"Amount paid ({p.CurrencyCode})", Money(p.Amount), true));
        return Compose(new Doc(d.Header, "Payment voucher", p.Number, Stamp(p.Status.ToString()),
            "Paid to", d.PartyName, PartyLines(d.PartyAddress, d.PartyTaxNumber, d.PartyPhone, d.PartyEmail),
            meta, AllocationColumns("Bill no."), rows, totals, null, []));
    }

    public byte[] Render(PrintJournalDto d)
    {
        var j = d.Journal;
        var meta = new List<(string, string)> { ("Journal no.", j.Number), ("Date", Date(j.Date)) };
        if (!string.IsNullOrWhiteSpace(j.Reference)) meta.Add(("Reference", j.Reference!));
        meta.Add(("Source", Humanize(j.SourceType.ToString())));
        meta.Add(("Currency", j.CurrencyCode));
        Column[] columns = [new("#", 0.5f), new("Account", 1.2f), new("Account name", 3f), new("Description", 3f), new("Debit", 1.6f, true), new("Credit", 1.6f, true)];
        var lines = d.Lines.OrderBy(l => l.Seq).ToList();
        var rows = lines.Select((l, n) => new[] { (n + 1).ToString(Inv), l.AccountCode, l.AccountName, l.Description ?? "", l.Debit == 0 ? "" : Money(l.Debit), l.Credit == 0 ? "" : Money(l.Credit) }).ToList();
        var status = d.Status.ToString();
        var stamp = status switch { "Draft" => "Draft", "Reversed" => "Reversed", _ => null };
        return Compose(new Doc(d.Header, "Journal entry voucher", j.Number, stamp, null, null, [], meta, columns, rows,
            [new($"Total debit ({j.CurrencyCode})", Money(lines.Sum(l => l.Debit)), true), new($"Total credit ({j.CurrencyCode})", Money(lines.Sum(l => l.Credit)), true)],
            j.Memo, []));
    }

    public byte[] Render(PrintGenericDto d)
        => Compose(new Doc(d.Header, d.Title, d.Number, d.Stamp, d.PartyLabel, d.PartyName, d.PartyLines, d.Meta,
            [.. d.Columns.Select(c => new Column(c.Title, c.Relative, c.Numeric))], d.Rows, [.. d.Totals.Select(t => new TotalLine(t.Label, t.Value, t.Strong))], d.Notes, []));

    // ----- shared model -----

    private sealed record Column(string Title, float Relative, bool Numeric = false);
    private sealed record TotalLine(string Label, string Value, bool Strong = false);

    private sealed record Doc(
        PrintHeaderDto Header, string Title, string Number, string? Stamp,
        string? PartyLabel, string? PartyName, IReadOnlyList<string> PartyLines,
        IReadOnlyList<(string Label, string Value)> Meta,
        IReadOnlyList<Column> Columns, IReadOnlyList<string[]> Rows,
        IReadOnlyList<TotalLine> Totals, string? Notes, IReadOnlyList<string> Terms);

    private static readonly Column[] LineColumns =
    [
        new("#", 0.5f), new("Description", 4.4f), new("Qty", 1f, true), new("Unit price", 1.5f, true),
        new("Taxable amount", 1.7f, true), new("Tax %", 0.9f, true), new("Tax", 1.3f, true), new("Total", 1.6f, true),
    ];

    private static Column[] AllocationColumns(string docLabel) => [new("#", 0.5f), new(docLabel, 6f), new("Amount applied", 2f, true)];

    private static string[] LineRow(int n, string description, decimal qty, decimal price, decimal net, decimal tax) =>
    [
        n.ToString(Inv), description, qty.ToString("0.####", Inv), Money(price), Money(net),
        net == 0 || tax == 0 ? "" : (tax / net * 100m).ToString("0.##", Inv) + "%", Money(tax), Money(net + tax),
    ];

    private static List<TotalLine> DocTotals(decimal net, decimal tax, decimal total, decimal paid, string currency)
    {
        var list = new List<TotalLine> { new("Total excluding tax", Money(net)), new("Tax", Money(tax)), new($"Total ({currency})", Money(total), true) };
        if (paid > 0)
        {
            list.Add(new("Amount paid", Money(paid)));
            list.Add(new($"Balance due ({currency})", Money(total - paid), true));
        }
        return list;
    }

    private static List<string> PartyLines(string? address, string? taxNo, string? phone, string? email)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(address)) lines.AddRange(address.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var contact = string.Join(" · ", new[] { phone, email }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (contact.Length > 0) lines.Add(contact);
        if (!string.IsNullOrWhiteSpace(taxNo)) lines.Add("Tax reg. no.: " + taxNo);
        return lines;
    }

    private static void AddInstrument(List<(string, string)> meta, object? kind, string? chequeNo, DateOnly? chequeDate, string? bank, object? status)
    {
        if (kind is not null) meta.Add(("Instrument", Humanize(kind.ToString())));
        if (!string.IsNullOrWhiteSpace(chequeNo)) meta.Add(("Cheque no.", chequeNo!));
        if (chequeDate is { } cd) meta.Add(("Cheque date", Date(cd)));
        if (!string.IsNullOrWhiteSpace(bank)) meta.Add(("Bank", bank!));
        if (status is not null) meta.Add(("Cheque status", Humanize(status.ToString())));
    }

    private static string? Stamp(string status) => status switch
    {
        "Draft" => "Draft",
        "PendingApproval" => "Pending approval",
        "Paid" => "Paid",
        "Voided" => "Void",
        _ => null,
    };

    private static string Money(decimal v) => v.ToString("N2", Inv);
    private static string Date(DateOnly d) => d.ToString("dd MMM yyyy", Inv);
    private static string Coalesce(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    /// <summary><c>BankTransfer</c> / <c>bankTransfer</c> → <c>Bank transfer</c>.</summary>
    internal static string Humanize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var spaced = Regex.Replace(value.Trim(), "(?<=[a-z0-9])(?=[A-Z])|[_-]+", " ").ToLowerInvariant();
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    // ----- layout -----

    private static byte[] Compose(Doc doc)
    {
        var h = doc.Header;
        var company = Coalesce(h.LegalEntityName, h.OrganizationName);
        return Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(14, Unit.Millimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(s => s.FontFamily(Fonts).FontSize(9.5f).FontColor(Ink));
                page.Header().Element(c => ComposeHeader(c, doc, company));
                page.Content().PaddingTop(10).Element(c => ComposeBody(c, doc, company));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            }))
            .WithMetadata(new DocumentMetadata { Title = $"{doc.Title} {doc.Number}", Author = company, Creator = company, Producer = company })
            .GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, Doc doc, string company)
    {
        var h = doc.Header;
        container.Column(col =>
        {
            col.Item().Height(3).Background(Accent);
            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(company).FontSize(15).Bold();
                    if (!string.IsNullOrWhiteSpace(h.LegalEntityAddress))
                        foreach (var line in h.LegalEntityAddress!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            left.Item().Text(line).FontColor(Muted);
                    var contact = string.Join(" · ", new[] { h.Phone, h.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (contact.Length > 0) left.Item().Text(contact).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(h.TaxRegistrationNo)) left.Item().Text("Tax reg. no.: " + h.TaxRegistrationNo).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(h.RegistrationNo)) left.Item().Text("Reg. no.: " + h.RegistrationNo).FontColor(Muted);
                });
                row.ConstantItem(190).Column(right =>
                {
                    right.Item().AlignRight().Text(doc.Title.ToUpperInvariant()).FontSize(16).Bold().FontColor(Accent);
                    right.Item().AlignRight().Text(doc.Number).FontSize(11).SemiBold();
                    if (doc.Stamp is { } stamp)
                    {
                        var color = stamp switch { "Paid" => Accent, "Void" or "Reversed" => Danger, _ => Warn };
                        right.Item().PaddingTop(4).AlignRight().Border(1).BorderColor(color).PaddingHorizontal(8).PaddingVertical(2)
                            .Text(stamp.ToUpperInvariant()).FontSize(9).Bold().FontColor(color);
                    }
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(0.75f).LineColor(Rule);
        });
    }

    private static void ComposeBody(IContainer container, Doc doc, string company)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            col.Item().Row(row =>
            {
                if (doc.PartyLabel is not null)
                {
                    row.RelativeItem().Column(p =>
                    {
                        p.Item().Text(doc.PartyLabel.ToUpperInvariant()).FontSize(8).SemiBold().FontColor(Muted);
                        p.Item().PaddingTop(2).Text(doc.PartyName ?? "").FontSize(11).SemiBold();
                        foreach (var line in doc.PartyLines) p.Item().Text(line).FontColor(Muted);
                    });
                    row.ConstantItem(16);
                }
                else
                {
                    row.RelativeItem();
                }
                row.ConstantItem(230).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(1.3f); });
                    foreach (var (label, value) in doc.Meta)
                    {
                        t.Cell().PaddingVertical(1.5f).Text(label).FontColor(Muted);
                        t.Cell().PaddingVertical(1.5f).AlignRight().Text(value).SemiBold();
                    }
                });
            });

            if (doc.Rows.Count > 0 || doc.Columns.Count > 0)
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { foreach (var column in doc.Columns) c.RelativeColumn(column.Relative); });
                    t.Header(head =>
                    {
                        foreach (var column in doc.Columns)
                        {
                            var cell = head.Cell().Background(HeadFill).PaddingVertical(4).PaddingHorizontal(4);
                            (column.Numeric ? cell.AlignRight() : cell).Text(column.Title).FontSize(8.5f).SemiBold().FontColor(Muted);
                        }
                    });
                    if (doc.Rows.Count == 0)
                    {
                        t.Cell().ColumnSpan((uint)doc.Columns.Count).Padding(6).AlignCenter().Text("No lines").FontColor(Muted);
                    }
                    foreach (var r in doc.Rows)
                    {
                        for (var i = 0; i < doc.Columns.Count; i++)
                        {
                            var cell = t.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(4).PaddingHorizontal(4);
                            (doc.Columns[i].Numeric ? cell.AlignRight() : cell).Text(i < r.Length ? r[i] : "");
                        }
                    }
                });
            }

            col.Item().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(250).Column(tot =>
                {
                    foreach (var line in doc.Totals)
                    {
                        var item = line.Strong ? tot.Item().BorderTop(0.75f).BorderColor(Ink).PaddingTop(3) : tot.Item().PaddingVertical(1.5f);
                        item.Row(r =>
                        {
                            var label = r.RelativeItem().Text(line.Label);
                            var value = r.AutoItem().Text(line.Value);
                            if (line.Strong) { label.Bold(); value.Bold().FontSize(11); }
                            else { label.FontColor(Muted); }
                        });
                    }
                });
            });

            if (!string.IsNullOrWhiteSpace(doc.Notes))
            {
                col.Item().Column(n =>
                {
                    n.Item().Text("NOTES").FontSize(8).SemiBold().FontColor(Muted);
                    n.Item().PaddingTop(2).Text(doc.Notes!);
                });
            }

            col.Item().PaddingTop(6).Column(terms =>
            {
                foreach (var line in doc.Terms) terms.Item().Text(line).FontColor(Muted);
                terms.Item().Text($"This document was generated by {company}.").FontSize(8).FontColor(Muted);
            });
        });
    }
}
