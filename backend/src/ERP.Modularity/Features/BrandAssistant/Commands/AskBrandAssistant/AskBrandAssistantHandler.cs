using System.Runtime.CompilerServices;
using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Application.Common;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;

namespace ERP.Modularity.Features;

internal sealed class AskBrandAssistantHandler(
    AssistantRunner runner, ISender sender, IBrandConversationRepository conversations, IBrandMessageRepository messages,
    IBrandSessionTokens tokens, IClock clock, IValidator<AskBrandAssistantCommand> validator) : IStreamRequestHandler<AskBrandAssistantCommand, AssistantEvent>
{
    private static readonly string[] FallbackCodes = ["daily-limit", "no-service", "unavailable"];

    public async IAsyncEnumerable<AssistantEvent> Handle(AskBrandAssistantCommand request, [EnumeratorCancellation] CancellationToken ct)
    {
        var status = await sender.Send(new GetBrandAssistantStatusQuery(), ct);
        if (!status.Visible) throw new NotFoundException();
        await validator.ValidateAndThrowAsync(request, ct);
        var text = request.Input.Text.Trim();

        // The hidden field only a bot fills in: finish politely and spend nothing.
        if (!string.IsNullOrWhiteSpace(request.Input.Website))
        {
            yield return new AssistantDone(0, 0);
            yield break;
        }

        var now = clock.UtcNow;
        var existing = tokens.Read(request.Input.Token) is { } id ? await conversations.FindForUpdateAsync(id, ct) : null;
        var conversation = existing ?? new BrandConversation { StartedAt = now, LastMessageAt = now };
        if (existing is null) conversations.Add(conversation);
        yield return new AssistantData("session", new { token = tokens.Issue(conversation.Id) });

        var config = await sender.Send(new GetSignupConfigQuery(), ct);
        var content = await sender.Send(new GetPublicContentQuery(), ct);
        if (conversation.VisitorMessages >= BrandAssistant.MaxVisitorMessages)
        {
            yield return new AssistantDelta(BrandAssistant.IsArabic(text)
                ? "وصلنا إلى نهاية هذه المحادثة. يسعد فريقنا مساعدتك في الباقي."
                : "We have reached the end of this chat. Our team would be glad to help with the rest.");
            yield return new AssistantData("contact", new { reason = "conversation-limit" });
            yield return new AssistantDone(0, 0);
            yield break;
        }

        var history = (await messages.ListLatestAsync(conversation.Id, BrandAssistant.HistoryMessages, ct))
            .OrderBy(m => m.At).Select(m => new ChatMessage(m.FromVisitor ? ChatRole.User : ChatRole.Assistant, m.Text)).Append(new ChatMessage(ChatRole.User, text)).ToList();

        conversation.VisitorMessages++;
        conversation.LastMessageAt = now;
        messages.Add(new BrandMessage { ConversationId = conversation.Id, At = now, FromVisitor = true, Text = text });
        await messages.SaveChangesAsync(ct);

        if (BrandAssistant.AsksAboutPrices(text) && config.Plans.Count > 0)
            yield return new AssistantData("plans", new { plans = config.Plans, config.Mode });

        var legal = await sender.Send(new GetLegalDocumentsQuery(), ct);
        var modules = await sender.Send(new ListPlatformModulesQuery(), ct);
        var industries = await sender.Send(new ListIndustriesQuery(), ct);
        var countries = await sender.Send(new ListCountriesQuery(), ct);
        var matches = BrandAssistant.FindFaqs(content.Faqs, text, 3);
        var system = BrandAssistant.Rules + "\n\n" + BrandAssistant.FactSheet(config, legal, content, modules, industries, countries, matches);
        IAssistantTool[] tools = [new RecommendPlanTool(config.Plans), new ReadPolicyTool(legal)];

        var reply = new StringBuilder();
        var fallback = false;
        try
        {
            await foreach (var e in runner.RunAsync(new AssistantTurn(AssistantBot.Brand, system, history, tools, BrandAssistant.MaxReplyTokens), ct))
            {
                switch (e)
                {
                    case AssistantServiceUsed:
                        continue; // visitors are never told which service answered
                    case AssistantDelta d:
                        reply.Append(d.Text);
                        break;
                    case AssistantError { Code: var code } when FallbackCodes.Contains(code):
                        fallback = true;
                        continue;
                }
                yield return e;
            }

            if (fallback)
            {
                var answer = Fallback(text, matches, status.SupportEmail);
                reply.Append(answer);
                yield return new AssistantDelta(answer);
                yield return new AssistantData("contact", new { reason = "unavailable" });
                yield return new AssistantDone(0, 0);
            }
        }
        finally
        {
            // Saved even when the visitor leaves part way through a reply.
            if (reply.Length > 0)
            {
                messages.Add(new BrandMessage { ConversationId = conversation.Id, At = clock.UtcNow, FromVisitor = false, Text = reply.ToString() });
                if (BrandAssistant.QuotesUnknownPrice(reply.ToString(), config.Plans)) conversation.NeedsReview = true;
                await messages.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>With no service available, the best written answer still helps. It is worded by people, so it cannot be wrong in the way a model can.</summary>
    private static string Fallback(string question, IReadOnlyList<PublicFaq> matches, string? supportEmail)
    {
        var arabic = BrandAssistant.IsArabic(question);
        var contact = supportEmail is { Length: > 0 }
            ? (arabic ? $" يمكنك أيضاً مراسلتنا على {supportEmail}." : $" You can also write to {supportEmail}.")
            : "";
        if (matches.Count > 0) return (arabic ? matches[0].AAr : matches[0].A) + contact;
        return (arabic ? "لا يمكنني الإجابة عن ذلك الآن. يسعد فريقنا مساعدتك." : "I cannot answer that right now. Our team would be glad to help.") + contact;
    }
}
