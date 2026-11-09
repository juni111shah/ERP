using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetAssistantConsoleHandler(AssistantConsole console) : IQueryHandler<GetAssistantConsoleQuery, AssistantConsoleView>
{
    public Task<AssistantConsoleView> Handle(GetAssistantConsoleQuery query, CancellationToken ct) => console.ViewAsync(ct);
}
