using ERP.Application.Common;

namespace ERP.Application.Printing;

internal sealed class EmailGenericDocumentHandler(DocumentEmailSender mail) : ICommandHandler<EmailGenericDocumentCommand, EmailSentDto>
{
    public async Task<EmailSentDto> Handle(EmailGenericDocumentCommand command, CancellationToken ct)
    {
        // 1. Email must be configured.
        mail.EnsureConfigured();

        // 2. The facts (party first), the first and last column of each row, and the totals.
        var d = command.Doc;
        var facts = new List<(string, string)>();
        if (d.PartyName is not null) facts.Add((d.PartyLabel ?? "To", d.PartyName));
        facts.AddRange(d.Meta);
        var lines = d.Rows.Select(r => (r.Length > 0 ? r[0] : "", "", "", r.Length > 1 ? r[^1] : "")).ToList();
        var doc = new EmailDoc($"{d.Title} {d.Number}", d.Header.LegalEntityName, d.Header.BaseCurrency, facts, lines, d.Totals.Select(t => (t.Label, t.Value)).ToList(), command.Message);

        // 3. Send it.
        return await mail.SendAsync(command.Kind, command.Id, doc, DocumentEmailSender.ResolveRecipient(command.To, null), ct);
    }
}
