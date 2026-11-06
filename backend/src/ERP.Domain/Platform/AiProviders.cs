using ERP.Domain.Common;

namespace ERP.Domain.Platform;

/// <summary>
/// An AI service the founder has connected. It belongs to the platform, not to a workspace. The key is stored encrypted and is never sent back to a browser.
/// </summary>
public class AiProvider : Entity, IAuditable
{
    public string Name { get; set; } = default!;
    /// <summary>The service's address up to, but not including, /chat/completions.</summary>
    public string BaseUrl { get; set; } = default!;
    public string Model { get; set; } = default!;
    public string EncryptedApiKey { get; set; } = default!;
    /// <summary>The last characters of the key, so the founder can tell keys apart.</summary>
    public string ApiKeyHint { get; set; } = default!;
    public bool UseForBrand { get; set; } = true;
    /// <summary>The workspace assistant sends client data, so it uses only the services the founder has cleared for that.</summary>
    public bool UseForWorkspace { get; set; }
    /// <summary>Lowest first. When a service fails or hits its limit, the assistant moves to the next one.</summary>
    public int SortOrder { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public bool? LastTestOk { get; set; }
    public string? LastTestMessage { get; set; }
    /// <summary>Real replies that failed in a row, leaving out reported limits. Enough of them marks the service as failed.</summary>
    public int ConsecutiveFailures { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>What one AI service used for one assistant on one day (UTC). It keeps the service's name, so the record outlives a deleted service.</summary>
public class AssistantUsageDay : Entity
{
    public DateOnly Day { get; set; }
    public string Bot { get; set; } = default!;
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = default!;
    public int Requests { get; set; }
    public int Failures { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public string? LastFailure { get; set; }
}
