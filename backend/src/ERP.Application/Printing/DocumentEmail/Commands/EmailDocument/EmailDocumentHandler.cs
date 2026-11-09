using System.Globalization;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using MediatR;

namespace ERP.Application.Printing;

internal sealed class EmailDocumentHandler(ISender sender, DocumentEmailSender mail) : ICommandHandler<EmailDocumentCommand, EmailSentDto>
{
    private static readonly DocumentStatus[] Sendable = [DocumentStatus.Issued, DocumentStatus.PartlyPaid, DocumentStatus.Paid];

    public async Task<EmailSentDto> Handle(EmailDocumentCommand command, CancellationToken ct)
    {
        // 1. Email must be configured.
        mail.EnsureConfigured();

        // 2. Render the document from its print data (which checks it exists and the caller may read it).
        var (doc, partyEmail) = command.Kind switch
        {
            EmailDocKind.Invoice => await InvoiceAsync(command.Id, command.Input.Message, ct),
            EmailDocKind.Receipt => await ReceiptAsync(command.Id, command.Input.Message, ct),
            EmailDocKind.Bill => await BillAsync(command.Id, command.Input.Message, ct),
            EmailDocKind.Payment => await PaymentAsync(command.Id, command.Input.Message, ct),
            _ => throw new FieldValidationException("kind", "Unsupported document."),
        };

        // 3. Send it to the requested address or the party's own.
        return await mail.SendAsync(command.Kind.ToString().ToLowerInvariant(), command.Id.ToString(), doc, DocumentEmailSender.ResolveRecipient(command.Input.To, partyEmail), ct);
    }

    private async Task<(EmailDoc, string?)> InvoiceAsync(Guid id, string? msg, CancellationToken ct)
    {
        mail.Require(CorePermissions.InvoicesRead);
        var p = await sender.Send(new GetPrintInvoiceQuery(id), ct);
        var i = p.Invoice;
        if (!Sendable.Contains(i.Status)) throw new ConflictException("Only issued documents can be emailed.");
        var kind = i.IsCreditNote ? "Credit note" : "Invoice";
        var facts = new List<(string, string)> { ("Billed to", i.PartyName ?? ""), ("Date", $"{i.InvoiceDate:yyyy-MM-dd}"), ("Due date", $"{i.DueDate:yyyy-MM-dd}") };
        if (!string.IsNullOrWhiteSpace(p.Header.TaxRegistrationNo)) facts.Add(("Tax registration", p.Header.TaxRegistrationNo!));
        var lines = p.Lines.Select(l => (l.Description, l.Quantity.ToString("0.####", CultureInfo.InvariantCulture), l.Price.ToString("N2", CultureInfo.InvariantCulture), l.Net.ToString("N2", CultureInfo.InvariantCulture))).ToList();
        var totals = new List<(string, string)> { ("Net", DocumentEmailRenderer.Money(i.Net, i.CurrencyCode)), ("Tax", DocumentEmailRenderer.Money(i.Tax, i.CurrencyCode)), ("Total", DocumentEmailRenderer.Money(i.Total, i.CurrencyCode)), ("Paid", DocumentEmailRenderer.Money(i.Paid, i.CurrencyCode)), ("Outstanding", DocumentEmailRenderer.Money(i.Outstanding, i.CurrencyCode)) };
        return (new EmailDoc($"{kind} {i.Number}", p.Header.LegalEntityName, i.CurrencyCode, facts, lines, totals, msg), p.PartyEmail);
    }

    private async Task<(EmailDoc, string?)> ReceiptAsync(Guid id, string? msg, CancellationToken ct)
    {
        mail.Require(CorePermissions.ReceiptsRead);
        var p = await sender.Send(new GetPrintReceiptQuery(id), ct);
        var r = p.Receipt;
        if (r.Status != DocumentStatus.Issued) throw new ConflictException("Only issued documents can be emailed.");
        var facts = new List<(string, string)> { ("Received from", p.PartyName), ("Date", $"{r.Date:yyyy-MM-dd}"), ("Method", r.Method.ToString()) };
        if (!string.IsNullOrWhiteSpace(r.Reference)) facts.Add(("Reference", r.Reference!));
        var lines = p.Allocations.Select(a => (string.IsNullOrEmpty(a.InvoiceNumber) ? (a.Label ?? "On account") : $"Applied to invoice {a.InvoiceNumber}", "", "", a.Amount.ToString("N2", CultureInfo.InvariantCulture))).ToList();
        return (new EmailDoc($"Receipt {r.Number}", p.Header.LegalEntityName, r.CurrencyCode, facts, lines, [("Amount received", DocumentEmailRenderer.Money(r.Amount, r.CurrencyCode))], msg), p.PartyEmail);
    }

    private async Task<(EmailDoc, string?)> BillAsync(Guid id, string? msg, CancellationToken ct)
    {
        mail.Require(CorePermissions.BillsRead);
        var p = await sender.Send(new GetPrintBillQuery(id), ct);
        var b = p.Bill;
        if (!Sendable.Contains(b.Status)) throw new ConflictException("Only posted documents can be emailed.");
        var facts = new List<(string, string)> { ("Vendor", p.PartyName), ("Bill date", $"{b.BillDate:yyyy-MM-dd}"), ("Due date", $"{b.DueDate:yyyy-MM-dd}") };
        if (!string.IsNullOrWhiteSpace(b.VendorReference)) facts.Add(("Your reference", b.VendorReference!));
        var lines = p.Lines.Select(l => (l.Description, l.Quantity.ToString("0.####", CultureInfo.InvariantCulture), l.Price.ToString("N2", CultureInfo.InvariantCulture), l.Net.ToString("N2", CultureInfo.InvariantCulture))).ToList();
        return (new EmailDoc($"Bill {b.Number}", p.Header.LegalEntityName, b.CurrencyCode, facts, lines,
            [("Total", DocumentEmailRenderer.Money(b.Total, b.CurrencyCode)), ("Paid", DocumentEmailRenderer.Money(b.Paid, b.CurrencyCode)), ("Outstanding", DocumentEmailRenderer.Money(b.Total - b.Paid, b.CurrencyCode))], msg), p.PartyEmail);
    }

    private async Task<(EmailDoc, string?)> PaymentAsync(Guid id, string? msg, CancellationToken ct)
    {
        mail.Require(CorePermissions.PaymentsRead);
        var p = await sender.Send(new GetPrintPaymentQuery(id), ct);
        var x = p.Payment;
        if (x.Status != DocumentStatus.Issued) throw new ConflictException("Only issued documents can be emailed.");
        var facts = new List<(string, string)> { ("Paid to", p.PartyName), ("Date", $"{x.Date:yyyy-MM-dd}"), ("Method", x.Method.ToString()) };
        if (!string.IsNullOrWhiteSpace(x.Reference)) facts.Add(("Reference", x.Reference!));
        var lines = p.Allocations.Select(a => (string.IsNullOrEmpty(a.BillNumber) ? (a.Label ?? "On account") : $"Settles bill {a.BillNumber}", "", "", a.Amount.ToString("N2", CultureInfo.InvariantCulture))).ToList();
        return (new EmailDoc($"Payment advice {x.Number}", p.Header.LegalEntityName, x.CurrencyCode, facts, lines, [("Amount paid", DocumentEmailRenderer.Money(x.Amount, x.CurrencyCode))], msg), p.PartyEmail);
    }
}
