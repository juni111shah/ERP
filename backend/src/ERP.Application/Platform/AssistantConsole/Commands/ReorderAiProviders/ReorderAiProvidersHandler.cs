using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class ReorderAiProvidersHandler(AssistantConsole console, IAiProviderRepository providers, PlatformLog log) : ICommandHandler<ReorderAiProvidersCommand, AssistantConsoleView>
{
    public async Task<AssistantConsoleView> Handle(ReorderAiProvidersCommand command, CancellationToken ct)
    {
        await console.RequireOwnerAsync(ct);
        var rows = await providers.ListForUpdateAsync(ct);
        if (rows.Count != command.Input.Ids.Count || rows.Any(p => !command.Input.Ids.Contains(p.Id))) throw new ConflictException("The list of services has changed. Reload the page and try again.");
        foreach (var p in rows) p.SortOrder = (command.Input.Ids.ToList().IndexOf(p.Id) + 1) * 10;
        log.Add("AI service order changed", null, null, string.Join(", ", rows.OrderBy(p => p.SortOrder).Select(p => p.Name)));
        await providers.SaveChangesAsync(ct);
        return await console.ViewAsync(ct);
    }
}
