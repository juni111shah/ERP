namespace ERP.Application.Assistant;

public sealed record AssistantTryMessage(ChatRole Role, string Text);
public sealed record AssistantTryInput(AssistantBot Bot, IReadOnlyList<AssistantTryMessage> Messages);
