using System.Runtime.CompilerServices;
using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Assistant;

/// <summary>
/// Produces one reply: checks the daily limit, tries the founder's services in order, and runs any tools the model asks for.
/// It moves to the next service only while nothing has been shown, so a reply is never answered twice. It logs no message text.
/// </summary>
public sealed class AssistantRunner(
    IReadRepository<AiProvider> providers, IReadRepository<PlatformSetting> platformSettings, IAiKeyProtector protector, IChatModelClient client, IAssistantUsageStore usage,
    IMemoryCache cache, IClock clock, ILogger<AssistantRunner> log)
{
    public const int MaxToolCallsPerTurn = 3;
    private static readonly TimeSpan MaxTurnTime = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxPatientWait = TimeSpan.FromSeconds(30);
    private const int MaxPatientWaits = 2;
    private static readonly TimeSpan MaxCooldown = TimeSpan.FromHours(1);

    private static string CooldownKey(Guid providerId) => $"assistant-cooldown:{providerId}";

    public async IAsyncEnumerable<AssistantEvent> RunAsync(AssistantTurn turn, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var settings = await platformSettings.Query().OrderBy(s => s.CreatedAt).FirstOrDefaultAsync(ct) ?? new PlatformSetting();
        var limit = turn.Bot == AssistantBot.Brand ? settings.BrandAssistantDailyTokens : settings.WorkspaceAssistantDailyTokens;
        if (await usage.TokensOnAsync(today, turn.Bot, ct) >= limit)
        {
            yield return new AssistantError("daily-limit", "The assistant has reached its limit for today. Please try again tomorrow.");
            yield break;
        }

        var query = providers.Query().OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt);
        var candidates = (await (turn.Bot == AssistantBot.Brand ? query.Where(p => p.UseForBrand) : query.Where(p => p.UseForWorkspace)).ToListAsync(ct))
            .Where(p => !cache.TryGetValue(CooldownKey(p.Id), out _)).ToList();
        if (candidates.Count == 0)
        {
            yield return new AssistantError("no-service", "The assistant is not available right now.");
            yield break;
        }

        using var turnTime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        turnTime.CancelAfter(MaxTurnTime);

        yield return new AssistantStatus("Thinking");

        var messages = new List<ChatMessage>(turn.History);
        var definitions = turn.Tools.Select(t => t.Definition).ToList();
        var index = 0;
        var toolCallsUsed = 0;
        var patientWaits = 0;
        var shownText = false;
        var announced = false;
        var inputTotal = 0;
        var outputTotal = 0;

        while (true)
        {
            if (index >= candidates.Count)
            {
                yield return new AssistantError("unavailable", "The assistant is not available right now. Please try again in a moment.");
                yield break;
            }
            var provider = candidates[index];
            var key = protector.TryUnprotect(provider.EncryptedApiKey);
            if (key is null)
            {
                await usage.RecordAsync(today, turn.Bot, provider.Id, provider.Name, 0, 0, "The stored key can no longer be read.", CancellationToken.None);
                await usage.ServiceFailedAsync(provider.Id, "The stored key can no longer be read. Paste it again.", true, clock.UtcNow, CancellationToken.None);
                index++;
                continue;
            }

            var request = new ChatRequest(turn.System, messages, toolCallsUsed < MaxToolCallsPerTurn ? definitions : [], turn.MaxOutputTokens);
            var calls = new List<ChatToolCall>();
            var roundText = new System.Text.StringBuilder();
            ChatUsage? reported = null;
            ChatServiceException? failure = null;
            var cancelled = false;
            var roundEnded = false;

            try
            {
                await using var stream = client.StreamAsync(new AiServiceConnection(provider.BaseUrl, provider.Model, key), request, turnTime.Token).GetAsyncEnumerator(turnTime.Token);
                while (true)
                {
                    try
                    {
                        if (!await stream.MoveNextAsync()) break;
                    }
                    catch (ChatServiceException e) { failure = e; break; }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { cancelled = true; break; }
                    catch (OperationCanceledException) { failure = new ChatServiceException(ChatFailureKind.TimedOut, "The service took too long to answer."); break; }

                    switch (stream.Current)
                    {
                        case ChatTextDelta delta:
                            if (!announced) { announced = true; yield return new AssistantServiceUsed(provider.Name); }
                            shownText = true;
                            roundText.Append(delta.Text);
                            yield return new AssistantDelta(delta.Text);
                            break;
                        case ChatToolCallReady call:
                            calls.Add(call.Call);
                            break;
                        case ChatUsage used:
                            reported = used;
                            break;
                    }
                }
                roundEnded = true;
            }
            finally
            {
                // The person left mid-reply, so the caller stopped reading and nothing below will run. What the service produced must still count,
                // or closing the page would be a way around the daily limit.
                if (!roundEnded) await usage.RecordAsync(today, turn.Bot, provider.Id, provider.Name, Estimate(request), Estimate(roundText.Length), null, CancellationToken.None);
            }

            if (failure is not null)
            {
                log.LogWarning("AI service {Service} failed: {Kind}", provider.Name, failure.Kind);
                // A service that failed part way still spent tokens on what it had written.
                await usage.RecordAsync(today, turn.Bot, provider.Id, provider.Name, roundText.Length > 0 ? Estimate(request) : 0, Estimate(roundText.Length), failure.Message, CancellationToken.None);
                // A free plan's per-minute limit clears within seconds. With no other service to try, a short wait beats telling the person to come back.
                if (failure is { Kind: ChatFailureKind.RateLimited, RetryAfter: { } shortWait } && shortWait <= MaxPatientWait && !shownText && index == candidates.Count - 1 && patientWaits < MaxPatientWaits)
                {
                    patientWaits++;
                    try { await Task.Delay(shortWait < TimeSpan.Zero ? TimeSpan.Zero : shortWait, turnTime.Token); }
                    catch (OperationCanceledException) { yield break; }
                    continue;
                }
                if (failure.Kind == ChatFailureKind.RateLimited)
                    cache.Set(CooldownKey(provider.Id), true, failure.RetryAfter is { } wait && wait > TimeSpan.Zero ? (wait < MaxCooldown ? wait : MaxCooldown) : DefaultCooldown);
                // A reported limit passes by itself, so it says nothing about the service's health. A rejected key will not fix itself, so the founder is told at once.
                else await usage.ServiceFailedAsync(provider.Id, failure.Message, failure.Kind == ChatFailureKind.KeyRejected, clock.UtcNow, CancellationToken.None);
                if (shownText)
                {
                    yield return new AssistantError("interrupted", "The reply was interrupted. Please try again.");
                    yield break;
                }
                index++;
                continue;
            }

            var input = reported?.InputTokens ?? Estimate(request);
            var output = reported?.OutputTokens ?? Estimate(roundText.Length + calls.Sum(c => c.ArgumentsJson.Length));
            inputTotal += input;
            outputTotal += output;
            await usage.RecordAsync(today, turn.Bot, provider.Id, provider.Name, input, output, null, CancellationToken.None);
            if (cancelled) yield break;
            if (provider.ConsecutiveFailures > 0 || provider.LastTestOk == false) await usage.ServiceWorkedAsync(provider.Id, clock.UtcNow, CancellationToken.None);

            if (calls.Count == 0)
            {
                yield return new AssistantDone(inputTotal, outputTotal);
                yield break;
            }

            if (!announced) { announced = true; yield return new AssistantServiceUsed(provider.Name); }
            messages.Add(new ChatMessage(ChatRole.Assistant, roundText.Length > 0 ? roundText.ToString() : null, calls));
            foreach (var call in calls)
            {
                var tool = turn.Tools.FirstOrDefault(t => t.Definition.Name == call.Name);
                string result;
                if (tool is null) result = "There is no tool with that name.";
                else if (++toolCallsUsed > MaxToolCallsPerTurn) result = "The limit of tool calls has been reached. Answer with what you already have.";
                else
                {
                    yield return new AssistantStatus(tool.StatusText);
                    result = await RunToolAsync(tool, call, turnTime.Token);
                }
                messages.Add(new ChatMessage(ChatRole.Tool, result, ToolCallId: call.Id));
            }
        }
    }

    private async Task<string> RunToolAsync(IAssistantTool tool, ChatToolCall call, CancellationToken ct)
    {
        try { return await tool.RunAsync(call.ArgumentsJson, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            log.LogWarning(e, "Assistant tool {Tool} failed", tool.Definition.Name);
            return "The lookup failed. Tell the person you could not check this.";
        }
    }

    /// <summary>Roughly four characters to a token, for services that report no usage.</summary>
    private static int Estimate(int characters) => (characters + 3) / 4;

    private static int Estimate(ChatRequest r) => Estimate(r.System.Length + r.Messages.Sum(m => (m.Text?.Length ?? 0) + (m.ToolCalls?.Sum(c => c.ArgumentsJson.Length) ?? 0)) + r.Tools.Sum(t => t.Description.Length + t.ParametersJsonSchema.Length));
}
