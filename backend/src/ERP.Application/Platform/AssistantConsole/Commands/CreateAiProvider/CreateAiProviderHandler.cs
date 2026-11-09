using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class CreateAiProviderHandler(AssistantConsole console, IAiProviderRepository providers, PlatformLog log) : ICommandHandler<CreateAiProviderCommand, AssistantConsoleView>
{
    public async Task<AssistantConsoleView> Handle(CreateAiProviderCommand command, CancellationToken ct)
    {
        await console.RequireOwnerAsync(ct);
        var last = await providers.LastSortOrderAsync(ct);
        var provider = new AiProvider { SortOrder = (last ?? 0) + 10 };
        AssistantConsole.Apply(provider, command.Input);
        console.SetKey(provider, command.Input.ApiKey!);
        providers.Add(provider);
        log.Add("AI service added", null, null, $"{provider.Name}, model {provider.Model}.");
        await providers.SaveChangesAsync(ct);
        return await console.ViewAsync(ct);
    }
}
