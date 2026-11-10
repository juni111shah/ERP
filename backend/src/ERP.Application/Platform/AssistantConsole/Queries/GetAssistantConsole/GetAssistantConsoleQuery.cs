using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The AI assistants' switches, connected services, daily limits and today's usage.</summary>
public sealed record GetAssistantConsoleQuery : IQuery<AssistantConsoleView>;
