namespace ERP.Application.Platform;

/// <summary>Signs which conversation a visitor is in, so one visitor cannot read or add to another's (implemented in Infrastructure).</summary>
public interface IBrandSessionTokens
{
    string Issue(Guid conversationId);
    Guid? Read(string? token);
}
