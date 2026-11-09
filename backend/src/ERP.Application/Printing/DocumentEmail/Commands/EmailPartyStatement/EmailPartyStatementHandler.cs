using System.Globalization;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using MediatR;

namespace ERP.Application.Printing;

internal sealed class EmailPartyStatementHandler(ISender sender, DocumentEmailSender mail, IReadRepository<Party> parties, IReadRepository<Organization> orgs, ICurrentUser user, IClock clock)
    : ICommandHandler<EmailPartyStatementCommand, EmailSentDto>
{
    public async Task<EmailSentDto> Handle(EmailPartyStatementCommand command, CancellationToken ct)
    {
        // 1. Email must be configured and the caller may read reports.
        mail.EnsureConfigured();
        mail.Require(CorePermissions.ReportsRead);

        // 2. The customer or vendor statement as of the date (today by default).
        var party = await parties.GetAsync(command.PartyId, ct);
        var asOf = command.AsOf ?? DateOnly.FromDateTime(clock.UtcNow);
        var customer = (party.Roles & PartyRoles.Customer) != 0;
        IReadOnlyList<AgeingItem> items; decimal unapplied, balance; string name;
        if (customer) { var s = await sender.Send(new GetCustomerStatementQuery(command.PartyId, asOf, null), ct); items = s.Items; unapplied = s.UnallocatedReceipts; balance = s.Balance; name = s.PartyName ?? party.Name; }
        else { var s = await sender.Send(new GetVendorStatementQuery(command.PartyId, asOf, null), ct); items = s.Items; unapplied = s.UnallocatedPayments; balance = s.Balance; name = s.PartyName ?? party.Name; }

        // 3. Render and send it.
        var company = await CompanyNameAsync(ct);
        var doc = new EmailDoc($"Statement of account as of {asOf:yyyy-MM-dd}", company, "", [("Account", name), ("As of", asOf.ToString("yyyy-MM-dd"))],
            items.Select(i => ($"{i.Number}{(i.IsCreditNote ? " (credit note)" : "")}, due {i.DueDate:yyyy-MM-dd}" + (i.DaysOverdue > 0 ? $", {i.DaysOverdue} days overdue" : ""), "", "", i.Outstanding.ToString("N2", CultureInfo.InvariantCulture))).ToList(),
            [.. (unapplied > 0 ? new[] { ("On account", unapplied.ToString("N2", CultureInfo.InvariantCulture)) } : []), ("Balance", balance.ToString("N2", CultureInfo.InvariantCulture))], command.Input.Message);
        return await mail.SendAsync("statement", command.PartyId.ToString(), doc, DocumentEmailSender.ResolveRecipient(command.Input.To, party.Email), ct);
    }

    private async Task<string> CompanyNameAsync(CancellationToken ct)
    {
        var orgId = user.OrgId ?? Guid.Empty;
        return (await orgs.FindAsync(orgId, ct))?.Name ?? "";
    }
}
