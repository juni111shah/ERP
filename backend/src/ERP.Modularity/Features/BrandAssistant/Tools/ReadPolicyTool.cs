using ERP.Application.Assistant;
using ERP.Application.Platform;

namespace ERP.Modularity.Features;

internal sealed class ReadPolicyTool(LegalDocuments legal) : IAssistantTool
{
    private const int MaxLength = 6000;

    public ChatToolDefinition Definition { get; } = new("read_policy",
        "Reads the Terms of Service or the Privacy Policy, for questions about them.",
        """{"type":"object","properties":{"kind":{"type":"string","enum":["terms","privacy"]}},"required":["kind"]}""");

    public string StatusText => "Reading the policy";

    public Task<string> RunAsync(string argumentsJson, CancellationToken ct)
    {
        var text = argumentsJson.Contains("privacy", StringComparison.OrdinalIgnoreCase) ? legal.Privacy : legal.Terms;
        return Task.FromResult(text.Length > MaxLength ? text[..MaxLength] : text);
    }
}
