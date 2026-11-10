using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class TestAiProviderHandler(AssistantConsole console, IAiProviderRepository providers, IAiKeyProtector protector, IAiConnectionTester tester, IClock clock)
    : ICommandHandler<TestAiProviderCommand, AssistantConsoleView>
{
    public async Task<AssistantConsoleView> Handle(TestAiProviderCommand command, CancellationToken ct)
    {
        await console.RequireOwnerAsync(ct);
        var provider = await providers.GetForUpdateAsync(command.Id, ct);
        var key = protector.TryUnprotect(provider.EncryptedApiKey);
        var result = key is null
            ? new AiConnectionResult(false, "The stored key can no longer be read. Paste it again.")
            : await tester.TestAsync(provider.BaseUrl, provider.Model, key, ct);
        provider.LastTestedAt = clock.UtcNow;
        provider.LastTestOk = result.Ok;
        provider.LastTestMessage = result.Message;
        await providers.SaveChangesAsync(ct);
        return await console.ViewAsync(ct);
    }
}
