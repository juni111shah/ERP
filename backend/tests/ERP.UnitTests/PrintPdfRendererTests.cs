using System.Text;
using ERP.Application.Finance;
using ERP.Application.Printing;
using ERP.Domain.Finance;
using ERP.Infrastructure.Printing;
using UglyToad.PdfPig;

namespace ERP.UnitTests;

/// <summary>The server-side PDF renderer: every print document becomes a real PDF carrying its number, party and totals.</summary>
public class PrintPdfRendererTests
{
    private static readonly QuestPdfPrintRenderer Renderer = new();
    private static readonly DateOnly Day = new(2026, 6, 1);

    private static PrintHeaderDto Header(string? taxNo = "TRN-123", string entity = "Northwind Trading LLC") =>
        new("Northwind Group", entity, "Office 12, Main Street\nHarbour City", taxNo, "REG-9", "+1 555 0100", "accounts@northwind.example", "USD", "US");

    private static string Text(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages()) sb.AppendLine(string.Join(" ", page.GetWords().Select(w => w.Text)));
        return sb.ToString();
    }

    private static void AssertPdf(byte[] pdf, params string[] expected)
    {
        Assert.True(pdf.Length > 1000, "PDF is suspiciously small");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var text = Text(pdf);
        foreach (var e in expected) Assert.Contains(e, text);
    }

    private static PrintInvoiceDto Invoice(string partyName = "Contoso Retail", string description = "Consulting hours", bool credit = false, decimal paid = 0m)
    {
        var lines = new List<InvoiceLineDto> { new(Guid.NewGuid(), 1, null, description, 2m, 500m, null, 50m, 1000m, null) };
        var inv = new InvoiceDto(Guid.NewGuid(), credit ? "CN-0007" : "INV-0042", credit, Guid.NewGuid(), Guid.NewGuid(), partyName, Day, Day.AddDays(30), "USD",
            1000m, 50m, 1050m, paid, paid > 0 ? DocumentStatus.PartlyPaid : DocumentStatus.Issued, null, null, null, "Deliver to the back door", lines);
        return new PrintInvoiceDto(Header(), inv, "PO Box 77\nRiverside", lines, "TAX-555", null, "ap@contoso.example", credit ? Guid.NewGuid() : null, credit ? "INV-0001" : null);
    }

    [Fact]
    public void Invoice_pdf_has_the_number_party_lines_and_total()
    {
        var pdf = Renderer.Render(Invoice(paid: 300m));
        AssertPdf(pdf, "INV-0042", "Contoso", "Consulting", "1,050.00", "TAX", "INVOICE", "750.00", "Northwind");
    }

    [Fact]
    public void Credit_note_pdf_is_titled_as_a_credit_note_and_names_the_original_invoice()
    {
        AssertPdf(Renderer.Render(Invoice(credit: true)), "CREDIT", "CN-0007", "INV-0001", "1,050.00");
    }

    [Fact]
    public void Receipt_pdf_has_the_number_allocations_and_amount()
    {
        var receipt = new ReceiptDto(Guid.NewGuid(), "RCT-0009", Guid.NewGuid(), Guid.NewGuid(), "Contoso Retail", Day, SettlementMethod.BankTransfer, Guid.NewGuid(), "USD",
            1200m, 150m, "Wire 88", DocumentStatus.Issued, null, [new AllocationDto(Guid.NewGuid(), Guid.NewGuid(), "INV-0042", 1050m, null)]);
        var dto = new PrintReceiptDto(Header(), receipt, "Contoso Retail", null, receipt.Allocations, null, null, null, null, null, null, null, null);
        AssertPdf(Renderer.Render(dto), "RCT-0009", "INV-0042", "1,200.00", "1,050.00", "150.00", "Bank transfer", "RECEIPT");
    }

    [Fact]
    public void Bill_pdf_has_the_number_vendor_reference_and_total()
    {
        var lines = new List<BillLineDto> { new(Guid.NewGuid(), 1, "Office supplies", 4m, 25m, null, 5m, true, 100m, null) };
        var bill = new BillDto(Guid.NewGuid(), "BILL-0003", Guid.NewGuid(), Guid.NewGuid(), "Fabrikam Supplies", "V-100", Day, Day.AddDays(15), "USD",
            100m, 5m, 105m, 0m, DocumentStatus.Draft, null, null, null, lines);
        var dto = new PrintBillDto(Header(), bill, "Fabrikam Supplies", null, lines, null, null, null);
        AssertPdf(Renderer.Render(dto), "BILL-0003", "V-100", "Fabrikam", "105.00", "DRAFT");
    }

    [Fact]
    public void Payment_pdf_has_the_voucher_number_bills_and_amount()
    {
        var payment = new PaymentDto(Guid.NewGuid(), "PAY-0011", Guid.NewGuid(), Guid.NewGuid(), "Fabrikam Supplies", Day, SettlementMethod.Cash, Guid.NewGuid(), "USD",
            105m, 0m, null, DocumentStatus.Issued, null, null, [new BillAllocationDto(Guid.NewGuid(), Guid.NewGuid(), "BILL-0003", 105m, null)]);
        var dto = new PrintPaymentDto(Header(), payment, "Fabrikam Supplies", payment.Allocations, null, null, null, null, null, null, null, null, null);
        AssertPdf(Renderer.Render(dto), "PAY-0011", "BILL-0003", "105.00", "VOUCHER");
    }

    [Fact]
    public void Journal_pdf_has_the_number_accounts_and_balanced_totals()
    {
        var lines = new List<JournalLineDto>
        {
            new(Guid.NewGuid(), 1, Guid.NewGuid(), "6900", "General expenses", 250m, 0m, "Accrual", null),
            new(Guid.NewGuid(), 2, Guid.NewGuid(), "2100", "Accrued liabilities", 0m, 250m, null, null),
        };
        var journal = new JournalDto(Guid.NewGuid(), "JV-0005", Guid.NewGuid(), Day, "Month-end accrual", "ME-06", "Manual", null, "USD",
            JournalStatus.Posted, 250m, null, null, lines);
        AssertPdf(Renderer.Render(new PrintJournalDto(Header(), journal, lines, JournalStatus.Posted)), "JV-0005", "6900", "2100", "250.00", "Month-end", "JOURNAL");
    }

    [Fact]
    public void Arabic_names_and_addresses_render_without_failing()
    {
        var inv = Invoice(partyName: "شركة النور للتجارة", description: "خدمات استشارية");
        var dto = inv with { Header = Header(entity: "مؤسسة المستقبل"), PartyAddress = "الدوحة، شارع الكورنيش" };
        AssertPdf(Renderer.Render(dto), "INV-0042", "1,050.00");
    }

    [Fact]
    public void A_header_without_tax_registration_prints_a_plain_invoice()
    {
        var dto = Invoice() with { Header = Header(taxNo: null) };
        var text = Text(Renderer.Render(dto));
        Assert.Contains("INVOICE", text);
        Assert.DoesNotContain("TAX INVOICE", text);
    }

    [Theory]
    [InlineData("Invoice", "INV-0001", "Invoice-INV-0001.pdf")]
    [InlineData("Receipt", "RCT/2026 01", "Receipt-RCT-2026-01.pdf")]
    [InlineData("Bill", "  ", "Bill-00000000.pdf")]
    [InlineData("Journal", "\"x\";\r\n", "Journal-x.pdf")]
    public void File_names_are_safe(string kind, string number, string expected) =>
        Assert.Equal(expected, PrintPdfNames.FileName(kind, number, Guid.Empty));
}
