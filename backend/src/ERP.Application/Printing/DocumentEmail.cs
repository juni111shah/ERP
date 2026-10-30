using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Printing;

public enum EmailDocKind { Invoice, Receipt, Bill, Payment, Statement }

/// <summary>Recipient (defaults to the party's email) and an optional short message shown above the document.</summary>
public sealed record EmailDocumentInput(string? To, string? Message);
public sealed record EmailSentDto(string To, string Subject);

/// <summary>Emails an issued invoice, receipt, bill or payment (remittance) to its party, rendered from the same data as the print page.</summary>
public sealed record EmailDocument(EmailDocKind Kind, Guid Id, EmailDocumentInput Input) : ICommand<EmailSentDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EmailSend;
}

/// <summary>Emails the party's open-items statement (customer statement for customers, vendor statement otherwise).</summary>
public sealed record EmailPartyStatement(Guid PartyId, DateOnly? AsOf, EmailDocumentInput Input) : ICommand<EmailSentDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EmailSend;
}

public sealed class EmailDocumentValidator : AbstractValidator<EmailDocument>
{
    public EmailDocumentValidator() { RuleFor(x => x.Input.To).MaximumLength(254); RuleFor(x => x.Input.Message).MaximumLength(1000); }
}

public sealed class EmailPartyStatementValidator : AbstractValidator<EmailPartyStatement>
{
    public EmailPartyStatementValidator() { RuleFor(x => x.Input.To).MaximumLength(254); RuleFor(x => x.Input.Message).MaximumLength(1000); }
}

/// <summary>Plain data a document is rendered from; HTML and text bodies are built from it so both always agree.</summary>
internal sealed record EmailDoc(string Title, string Company, string Currency, IReadOnlyList<(string Label, string Value)> Facts,
    IReadOnlyList<(string Description, string Qty, string Price, string Amount)> Lines, IReadOnlyList<(string Label, string Value)> Totals, string? Message);

internal static class DocumentEmailRenderer
{
    public static string Money(decimal v, string currency) => $"{v.ToString("N2", CultureInfo.InvariantCulture)} {currency}";

    public static string Text(EmailDoc d)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(d.Message)) sb.AppendLine(d.Message).AppendLine();
        sb.AppendLine(d.Title).AppendLine(d.Company).AppendLine();
        foreach (var (l, v) in d.Facts) sb.AppendLine($"{l}: {v}");
        if (d.Lines.Count > 0) { sb.AppendLine(); foreach (var l in d.Lines) sb.AppendLine($"- {l.Description}  {l.Qty} x {l.Price} = {l.Amount}"); }
        if (d.Totals.Count > 0) { sb.AppendLine(); foreach (var (l, v) in d.Totals) sb.AppendLine($"{l}: {v}"); }
        return sb.ToString().TrimEnd();
    }

    public static string Html(EmailDoc d)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937;max-width:640px\">");
        if (!string.IsNullOrWhiteSpace(d.Message)) sb.Append($"<p>{E(d.Message).Replace("\n", "<br>")}</p>");
        sb.Append($"<h2 style=\"margin:16px 0 4px\">{E(d.Title)}</h2><div style=\"color:#6b7280\">{E(d.Company)}</div>");
        sb.Append("<table style=\"margin-top:12px;border-collapse:collapse\">");
        foreach (var (l, v) in d.Facts) sb.Append($"<tr><td style=\"padding:2px 16px 2px 0;color:#6b7280\">{E(l)}</td><td>{E(v)}</td></tr>");
        sb.Append("</table>");
        if (d.Lines.Count > 0)
        {
            sb.Append("<table style=\"margin-top:16px;border-collapse:collapse;width:100%\"><tr style=\"background:#f3f4f6\"><th align=\"left\" style=\"padding:6px\">Description</th><th align=\"right\">Qty</th><th align=\"right\">Price</th><th align=\"right\" style=\"padding-right:6px\">Amount</th></tr>");
            foreach (var l in d.Lines) sb.Append($"<tr style=\"border-bottom:1px solid #e5e7eb\"><td style=\"padding:6px\">{E(l.Description)}</td><td align=\"right\">{E(l.Qty)}</td><td align=\"right\">{E(l.Price)}</td><td align=\"right\" style=\"padding-right:6px\">{E(l.Amount)}</td></tr>");
            sb.Append("</table>");
        }
        if (d.Totals.Count > 0)
        {
            sb.Append("<table style=\"margin-top:12px;margin-left:auto;border-collapse:collapse\">");
            foreach (var (l, v) in d.Totals) sb.Append($"<tr><td style=\"padding:2px 16px 2px 0;color:#6b7280\">{E(l)}</td><td align=\"right\"><b>{E(v)}</b></td></tr>");
            sb.Append("</table>");
        }
        return sb.Append("</div>").ToString();
    }
}

internal sealed class DocumentEmailHandlers(ISender sender, IEmailSender email, IRepository<MessageLog> log, IReadRepository<Domain.Parties.Party> parties, IReadRepository<Domain.Organizations.Organization> orgs, ICurrentUser user, IClock clock) :
    IRequestHandler<EmailDocument, EmailSentDto>,
    IRequestHandler<EmailPartyStatement, EmailSentDto>
{
    private static readonly DocumentStatus[] Sendable = [DocumentStatus.Issued, DocumentStatus.PartlyPaid, DocumentStatus.Paid];

    public async Task<EmailSentDto> Handle(EmailDocument cmd, CancellationToken ct)
    {
        EnsureConfigured();
        var (doc, partyEmail) = cmd.Kind switch
        {
            EmailDocKind.Invoice => await InvoiceAsync(cmd.Id, cmd.Input.Message, ct),
            EmailDocKind.Receipt => await ReceiptAsync(cmd.Id, cmd.Input.Message, ct),
            EmailDocKind.Bill => await BillAsync(cmd.Id, cmd.Input.Message, ct),
            EmailDocKind.Payment => await PaymentAsync(cmd.Id, cmd.Input.Message, ct),
            _ => throw new FieldValidationException("kind", "Unsupported document."),
        };
        return await SendAsync(cmd.Kind.ToString().ToLowerInvariant(), cmd.Id.ToString(), doc, ResolveRecipient(cmd.Input.To, partyEmail), ct);
    }

    public async Task<EmailSentDto> Handle(EmailPartyStatement cmd, CancellationToken ct)
    {
        EnsureConfigured();
        Require(CorePermissions.ReportsRead);
        var party = await parties.GetAsync(cmd.PartyId, ct);
        var asOf = cmd.AsOf ?? DateOnly.FromDateTime(clock.UtcNow);
        var customer = (party.Roles & Domain.Parties.PartyRoles.Customer) != 0;
        IReadOnlyList<AgeingItem> items; decimal unapplied, balance; string name;
        if (customer) { var s = await sender.Send(new GetCustomerStatement(cmd.PartyId, asOf, null), ct); items = s.Items; unapplied = s.UnallocatedReceipts; balance = s.Balance; name = s.PartyName ?? party.Name; }
        else { var s = await sender.Send(new GetVendorStatement(cmd.PartyId, asOf, null), ct); items = s.Items; unapplied = s.UnallocatedPayments; balance = s.Balance; name = s.PartyName ?? party.Name; }
        var company = await CompanyNameAsync(ct);
        var doc = new EmailDoc($"Statement of account as of {asOf:yyyy-MM-dd}", company, "", [("Account", name), ("As of", asOf.ToString("yyyy-MM-dd"))],
            items.Select(i => ($"{i.Number}{(i.IsCreditNote ? " (credit note)" : "")}, due {i.DueDate:yyyy-MM-dd}" + (i.DaysOverdue > 0 ? $", {i.DaysOverdue} days overdue" : ""), "", "", i.Outstanding.ToString("N2", CultureInfo.InvariantCulture))).ToList(),
            [.. (unapplied > 0 ? new[] { ("On account", unapplied.ToString("N2", CultureInfo.InvariantCulture)) } : []), ("Balance", balance.ToString("N2", CultureInfo.InvariantCulture))], cmd.Input.Message);
        return await SendAsync("statement", cmd.PartyId.ToString(), doc, ResolveRecipient(cmd.Input.To, party.Email), ct);
    }

    private async Task<(EmailDoc, string?)> InvoiceAsync(Guid id, string? msg, CancellationToken ct)
    {
        Require(CorePermissions.InvoicesRead);
        var p = await sender.Send(new GetPrintInvoice(id), ct);
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
        Require(CorePermissions.ReceiptsRead);
        var p = await sender.Send(new GetPrintReceipt(id), ct);
        var r = p.Receipt;
        if (r.Status != DocumentStatus.Issued) throw new ConflictException("Only issued documents can be emailed.");
        var facts = new List<(string, string)> { ("Received from", p.PartyName), ("Date", $"{r.Date:yyyy-MM-dd}"), ("Method", r.Method.ToString()) };
        if (!string.IsNullOrWhiteSpace(r.Reference)) facts.Add(("Reference", r.Reference!));
        var lines = p.Allocations.Select(a => (string.IsNullOrEmpty(a.InvoiceNumber) ? (a.Label ?? "On account") : $"Applied to invoice {a.InvoiceNumber}", "", "", a.Amount.ToString("N2", CultureInfo.InvariantCulture))).ToList();
        return (new EmailDoc($"Receipt {r.Number}", p.Header.LegalEntityName, r.CurrencyCode, facts, lines, [("Amount received", DocumentEmailRenderer.Money(r.Amount, r.CurrencyCode))], msg), p.PartyEmail);
    }

    private async Task<(EmailDoc, string?)> BillAsync(Guid id, string? msg, CancellationToken ct)
    {
        Require(CorePermissions.BillsRead);
        var p = await sender.Send(new GetPrintBill(id), ct);
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
        Require(CorePermissions.PaymentsRead);
        var p = await sender.Send(new GetPrintPayment(id), ct);
        var x = p.Payment;
        if (x.Status != DocumentStatus.Issued) throw new ConflictException("Only issued documents can be emailed.");
        var facts = new List<(string, string)> { ("Paid to", p.PartyName), ("Date", $"{x.Date:yyyy-MM-dd}"), ("Method", x.Method.ToString()) };
        if (!string.IsNullOrWhiteSpace(x.Reference)) facts.Add(("Reference", x.Reference!));
        var lines = p.Allocations.Select(a => (string.IsNullOrEmpty(a.BillNumber) ? (a.Label ?? "On account") : $"Settles bill {a.BillNumber}", "", "", a.Amount.ToString("N2", CultureInfo.InvariantCulture))).ToList();
        return (new EmailDoc($"Payment advice {x.Number}", p.Header.LegalEntityName, x.CurrencyCode, facts, lines, [("Amount paid", DocumentEmailRenderer.Money(x.Amount, x.CurrencyCode))], msg), p.PartyEmail);
    }

    private async Task<string> CompanyNameAsync(CancellationToken ct)
    {
        var orgId = user.OrgId ?? Guid.Empty;
        return await orgs.Query(new Specification<Domain.Organizations.Organization>(o => o.Id == orgId)).Select(o => o.Name).FirstOrDefaultAsync(ct) ?? "";
    }

    private void EnsureConfigured()
    {
        if (!email.IsEnabled) throw new ConflictException("Email is not configured for this system. Ask an administrator to set the Email settings.");
    }

    private void Require(string permission)
    {
        if (!user.HasPermission(permission)) throw new ForbiddenException();
    }

    private static string ResolveRecipient(string? requested, string? partyEmail)
    {
        var to = string.IsNullOrWhiteSpace(requested) ? partyEmail : requested;
        if (string.IsNullOrWhiteSpace(to)) throw new FieldValidationException("to", "This party has no email address. Enter one to send to.");
        try { _ = new MailAddress(to.Trim()); } catch (FormatException) { throw new FieldValidationException("to", "Enter a valid email address."); }
        return to.Trim();
    }

    private async Task<EmailSentDto> SendAsync(string kind, string id, EmailDoc doc, string to, CancellationToken ct)
    {
        var subject = doc.Title + (string.IsNullOrWhiteSpace(doc.Company) ? "" : $" - {doc.Company}");
        var text = DocumentEmailRenderer.Text(doc);
        var entry = new MessageLog
        {
            Channel = "email", Recipient = to, Subject = subject.Length > 400 ? subject[..400] : subject, Body = text.Length > 2000 ? text[..2000] : text,
            DedupeKey = $"doc-email:{kind}:{id}:{Guid.NewGuid():N}", SentAt = clock.UtcNow, Attempts = 1, Status = MessageStatus.Sent,
        };
        try { await email.SendAsync(to, subject, text, DocumentEmailRenderer.Html(doc), ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            entry.Status = MessageStatus.Failed; entry.Error = e.Message.Length > 400 ? e.Message[..400] : e.Message;
            log.Add(entry);
            await log.SaveChangesAsync(ct);
            throw new ConflictException($"The email could not be sent: {e.Message}");
        }
        log.Add(entry);
        await log.SaveChangesAsync(ct);
        return new(to, subject);
    }
}
