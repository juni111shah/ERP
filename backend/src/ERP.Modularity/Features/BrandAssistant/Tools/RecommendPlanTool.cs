using System.Text.Json;
using ERP.Application.Assistant;
using ERP.Application.Platform;

namespace ERP.Modularity.Features;

internal sealed class RecommendPlanTool(IReadOnlyList<PublicPlan> plans) : IAssistantTool
{
    public ChatToolDefinition Definition { get; } = new("recommend_plan",
        "Finds the cheapest plan that fits a business. Use it whenever someone asks which plan they need.",
        """{"type":"object","properties":{"users":{"type":"integer","description":"How many people will use it"},"legal_entities":{"type":"integer","description":"How many separate companies"},"modules":{"type":"array","items":{"type":"string"},"description":"Module names they need, if they said"}}}""");

    public string StatusText => "Checking the plans";

    public Task<string> RunAsync(string argumentsJson, CancellationToken ct)
    {
        int? users = null, entities = null;
        var modules = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("users", out var u) && u.TryGetInt32(out var un)) users = un;
            if (doc.RootElement.TryGetProperty("legal_entities", out var e) && e.TryGetInt32(out var en)) entities = en;
            if (doc.RootElement.TryGetProperty("modules", out var m) && m.ValueKind == JsonValueKind.Array) modules.AddRange(m.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0));
        }
        catch (JsonException) { return Task.FromResult("The request could not be read. Ask how many users and companies they have."); }

        var plan = BrandAssistant.Recommend(plans, users, entities, modules);
        return Task.FromResult(plan is null ? "No published plan fits this. Suggest they contact the team, who can help with larger needs." : "Recommended:\n" + BrandAssistant.Describe(plan));
    }
}
