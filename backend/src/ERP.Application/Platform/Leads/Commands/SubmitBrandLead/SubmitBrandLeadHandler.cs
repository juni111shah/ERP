using ERP.Application.Common;
using ERP.Domain.Platform;
using MediatR;

namespace ERP.Application.Platform;

internal sealed class SubmitBrandLeadHandler(IBrandLeadRepository leads, PlatformSettingsStore settingsStore, IBrandSessionTokens tokens, PlatformLog log, IPlatformMailer mailer)
    : ICommandHandler<SubmitBrandLeadCommand>
{
    public async Task<Unit> Handle(SubmitBrandLeadCommand command, CancellationToken ct)
    {
        // 1. Only while the public assistant is shown.
        var i = command.Input;
        var settings = await settingsStore.GetAsync(ct);
        if (!settings.BrandAssistantEnabled) throw new NotFoundException();
        // The hidden field only a bot fills in: answer as usual and keep nothing.
        if (!string.IsNullOrWhiteSpace(i.Website)) return Unit.Value;

        // 2. Keep the lead, linked to the visitor's conversation.
        var lead = new BrandLead
        {
            ConversationId = tokens.Read(i.Token), Status = BrandLeadStatus.New, Name = i.Name.Trim(), Email = i.Email.Trim().ToLowerInvariant(),
            Phone = Clean(i.Phone), Company = Clean(i.Company), Need = Clean(i.Need),
        };
        leads.Add(lead);
        log.Add("Lead received from the public assistant", null, lead.Company, lead.Email);
        await leads.SaveChangesAsync(ct);

        // 3. Tell the founder.
        await mailer.SendAsync(settings.SupportEmail, $"New lead: {lead.Name}",
            $"{lead.Name} ({lead.Email}) asked to be contacted through the assistant.\nCompany: {lead.Company ?? "not given"}\nPhone: {lead.Phone ?? "not given"}\n\n{lead.Need}\n\nSee it in your console: {mailer.Link("/platform/leads")}", ct);
        return Unit.Value;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
