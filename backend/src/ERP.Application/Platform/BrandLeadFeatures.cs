using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Platform;

/// <summary>Signs which conversation a visitor is in, so one visitor cannot read or add to another's (implemented in Infrastructure).</summary>
public interface IBrandSessionTokens
{
    string Issue(Guid conversationId);
    Guid? Read(string? token);
}

public sealed record BrandLeadInput(string? Token, string Name, string Email, string? Phone, string? Company, string? Need, bool Consent, string? Website);
public sealed record SubmitBrandLead(BrandLeadInput Input) : ICommand;

public sealed record BrandLeadRow(Guid Id, DateTime CreatedAt, BrandLeadStatus Status, string Name, string Email, string? Phone, string? Company, string? Need, Guid? ConversationId, bool HasRegistered);
public sealed record ListBrandLeads : PagedQuery, IQuery<PagedResult<BrandLeadRow>>
{
    public string? Status { get; init; }
}
public sealed record SetBrandLeadStatus(Guid Id, BrandLeadStatus Status) : ICommand<BrandLeadRow>;
public sealed record DeleteBrandLead(Guid Id) : ICommand;

public sealed record BrandConversationRow(Guid Id, DateTime StartedAt, DateTime LastMessageAt, int VisitorMessages, bool NeedsReview, string? FirstMessage);
public sealed record ListBrandConversations : PagedQuery, IQuery<PagedResult<BrandConversationRow>>;
public sealed record BrandMessageRow(DateTime At, bool FromVisitor, string Text);
public sealed record GetBrandConversation(Guid Id) : IQuery<IReadOnlyList<BrandMessageRow>>;
public sealed record MarkBrandConversationReviewed(Guid Id) : ICommand;
public sealed record DeleteBrandConversation(Guid Id) : ICommand;

public sealed record BrandAssistantStatus(bool Visible, string? SupportEmail);
public sealed record GetBrandAssistantStatus : IQuery<BrandAssistantStatus>;

public sealed class SubmitBrandLeadValidator : AbstractValidator<SubmitBrandLead>
{
    public SubmitBrandLeadValidator()
    {
        RuleFor(x => x.Input.Name).NotEmpty().WithMessage("Enter your name.").MaximumLength(120);
        RuleFor(x => x.Input.Email).NotEmpty().WithMessage("Enter your email.").EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(256);
        RuleFor(x => x.Input.Phone).MaximumLength(40);
        RuleFor(x => x.Input.Company).MaximumLength(200);
        RuleFor(x => x.Input.Need).MaximumLength(1000);
        RuleFor(x => x.Input.Consent).Equal(true).WithMessage("Please agree so that we can contact you.");
    }
}

internal sealed class BrandLeadHandlers(
    IRepository<BrandLead> leads, IRepository<BrandConversation> conversations, IRepository<BrandMessage> messages, IReadRepository<SignupRequest> signups,
    PlatformSettingsStore settingsStore, IBrandSessionTokens tokens, PlatformLog log, IPlatformMailer mailer, IPlatformDirectory directory, ICurrentUser user) :
    IRequestHandler<SubmitBrandLead, Unit>,
    IRequestHandler<ListBrandLeads, PagedResult<BrandLeadRow>>,
    IRequestHandler<SetBrandLeadStatus, BrandLeadRow>,
    IRequestHandler<DeleteBrandLead, Unit>,
    IRequestHandler<ListBrandConversations, PagedResult<BrandConversationRow>>,
    IRequestHandler<GetBrandConversation, IReadOnlyList<BrandMessageRow>>,
    IRequestHandler<MarkBrandConversationReviewed, Unit>,
    IRequestHandler<DeleteBrandConversation, Unit>,
    IRequestHandler<GetBrandAssistantStatus, BrandAssistantStatus>
{
    private async Task RequireOwnerAsync(CancellationToken ct)
    {
        if (user.UserId is not { } id || !await directory.IsPlatformOwnerAsync(id, ct)) throw new ForbiddenException("Only the platform owner can change public chats.");
    }

    public async Task<Unit> Handle(SubmitBrandLead cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var settings = await settingsStore.GetAsync(ct);
        if (!settings.BrandAssistantEnabled) throw new NotFoundException();
        // The hidden field only a bot fills in: answer as usual and keep nothing.
        if (!string.IsNullOrWhiteSpace(i.Website)) return Unit.Value;

        var lead = new BrandLead
        {
            ConversationId = tokens.Read(i.Token), Status = BrandLeadStatus.New, Name = i.Name.Trim(), Email = i.Email.Trim().ToLowerInvariant(),
            Phone = Clean(i.Phone), Company = Clean(i.Company), Need = Clean(i.Need),
        };
        leads.Add(lead);
        log.Add("Lead received from the public assistant", null, lead.Company, lead.Email);
        await leads.SaveChangesAsync(ct);
        await mailer.SendAsync(settings.SupportEmail, $"New lead: {lead.Name}",
            $"{lead.Name} ({lead.Email}) asked to be contacted through the assistant.\nCompany: {lead.Company ?? "not given"}\nPhone: {lead.Phone ?? "not given"}\n\n{lead.Need}\n\nSee it in your console: {mailer.Link("/platform/leads")}", ct);
        return Unit.Value;
    }

    public async Task<PagedResult<BrandLeadRow>> Handle(ListBrandLeads q, CancellationToken ct)
    {
        var query = leads.Query();
        if (Enum.TryParse<BrandLeadStatus>(q.Status, true, out var status)) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(l => l.Name.Contains(q.Search) || l.Email.Contains(q.Search) || (l.Company != null && l.Company.Contains(q.Search)));
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(l => l.CreatedAt).Skip((q.PageNumber - 1) * q.Size).Take(q.Size).ToListAsync(ct);
        var emails = page.Select(l => l.Email).ToList();
        var registered = await signups.Query().Where(s => emails.Contains(s.Email)).Select(s => s.Email).Distinct().ToListAsync(ct);
        return new(page.Select(l => ToRow(l, registered.Contains(l.Email))).ToList(), total, q.PageNumber, q.Size);
    }

    public async Task<BrandLeadRow> Handle(SetBrandLeadStatus cmd, CancellationToken ct)
    {
        var lead = await leads.GetForUpdateAsync(cmd.Id, ct);
        lead.Status = cmd.Status;
        log.Add($"Lead marked {cmd.Status.ToString().ToLowerInvariant()}", null, lead.Company, lead.Email);
        await leads.SaveChangesAsync(ct);
        return ToRow(lead, await signups.AnyAsync(s => s.Email == lead.Email, ct));
    }

    public async Task<Unit> Handle(DeleteBrandLead cmd, CancellationToken ct)
    {
        var lead = await leads.GetForUpdateAsync(cmd.Id, ct);
        leads.Remove(lead);
        log.Add("Lead deleted", null, lead.Company, null);
        await leads.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<PagedResult<BrandConversationRow>> Handle(ListBrandConversations q, CancellationToken ct)
    {
        var query = conversations.Query();
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(c => c.NeedsReview).ThenByDescending(c => c.LastMessageAt).Skip((q.PageNumber - 1) * q.Size).Take(q.Size).ToListAsync(ct);
        var ids = page.Select(c => c.Id).ToList();
        var first = (await messages.Query().Where(m => ids.Contains(m.ConversationId) && m.FromVisitor).OrderBy(m => m.At).Select(m => new { m.ConversationId, m.Text }).ToListAsync(ct))
            .GroupBy(m => m.ConversationId).ToDictionary(g => g.Key, g => g.First().Text);
        return new(page.Select(c => new BrandConversationRow(c.Id, c.StartedAt, c.LastMessageAt, c.VisitorMessages, c.NeedsReview, first.GetValueOrDefault(c.Id))).ToList(), total, q.PageNumber, q.Size);
    }

    public async Task<IReadOnlyList<BrandMessageRow>> Handle(GetBrandConversation q, CancellationToken ct)
        => await messages.Query().Where(m => m.ConversationId == q.Id).OrderBy(m => m.At).Select(m => new BrandMessageRow(m.At, m.FromVisitor, m.Text)).ToListAsync(ct);

    public async Task<Unit> Handle(MarkBrandConversationReviewed cmd, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var conversation = await conversations.GetForUpdateAsync(cmd.Id, ct);
        conversation.NeedsReview = false;
        log.Add("Public chat marked as reviewed", null, null);
        await conversations.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteBrandConversation cmd, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var conversation = await conversations.GetForUpdateAsync(cmd.Id, ct);
        foreach (var m in await messages.Tracked().Where(m => m.ConversationId == cmd.Id).ToListAsync(ct)) messages.Remove(m);
        conversations.Remove(conversation);
        log.Add("Public chat deleted", null, null);
        await conversations.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<BrandAssistantStatus> Handle(GetBrandAssistantStatus q, CancellationToken ct)
    {
        var s = await settingsStore.GetAsync(ct);
        return new(s.BrandAssistantEnabled, s.SupportEmail);
    }

    private static BrandLeadRow ToRow(BrandLead l, bool registered) => new(l.Id, l.CreatedAt, l.Status, l.Name, l.Email, l.Phone, l.Company, l.Need, l.ConversationId, registered);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Work that belongs to the platform rather than to a workspace. It runs once on each pass of the scheduler.</summary>
public interface IPlatformJob
{
    Task RunAsync(DateOnly today, CancellationToken ct);
}

/// <summary>Deletes public assistant chats after 30 days. Leads are kept until the founder deletes them.</summary>
internal sealed class BrandConversationCleanupJob(IRepository<BrandConversation> conversations, IRepository<BrandMessage> messages) : IPlatformJob
{
    public const int RetentionDays = 30;

    public async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        var cutoff = today.AddDays(-RetentionDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var old = await conversations.Tracked().Where(c => c.LastMessageAt < cutoff).Take(1000).ToListAsync(ct);
        if (old.Count == 0) return;
        var ids = old.Select(c => c.Id).ToList();
        foreach (var m in await messages.Tracked().Where(m => ids.Contains(m.ConversationId)).ToListAsync(ct)) messages.Remove(m);
        foreach (var c in old) conversations.Remove(c);
        await conversations.SaveChangesAsync(ct);
    }
}
