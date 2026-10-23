using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Customization;
using ERP.Domain.Finance;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Abstractions;

/// <summary>The signed-in caller. Org comes from the token (never from the request body).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? OrgId { get; }
    string? Email { get; }
    /// <summary>Legal entity selected with the X-Entity-Id header (null = all allowed entities).</summary>
    Guid? LegalEntityId { get; }
    /// <summary>Null = all legal entities of the workspace.</summary>
    IReadOnlyCollection<Guid>? AllowedLegalEntityIds { get; }
    IReadOnlySet<string> Permissions { get; }
    string? IpAddress { get; }
    bool HasPermission(string permission);
    /// <summary>Set only for tenant-portal sessions: the customer party the caller signed in as.</summary>
    Guid? PortalPartyId => null;
}

/// <summary>
/// Organization scope used by EF global filters. Normally the caller's org; trusted system code (login,
/// onboarding, background jobs) can switch it explicitly. A null org reads nothing (fail closed).
/// </summary>
public interface IOrgContext
{
    Guid? OrgId { get; }
    Guid? LegalEntityId { get; }
    IReadOnlyCollection<Guid>? AllowedLegalEntityIds { get; }
    IDisposable Use(Guid orgId);
    void SetLegalEntity(Guid? legalEntityId);
}

public interface IClock { DateTime UtcNow { get; } }

public interface INumberingService
{
    Task<string> NextAsync(string documentType, Guid? legalEntityId = null, DateOnly? date = null, CancellationToken ct = default);
}

public interface IApprovalService
{
    Task<bool> RequiresApprovalAsync(string documentType, decimal? amount, CancellationToken ct = default);
    Task<ApprovalRequest> SubmitAsync(string documentType, Guid? documentId, string subject, decimal? amount, string? justification, Guid? legalEntityId, CancellationToken ct = default);
    Task<ApprovalRequest> DecideAsync(Guid requestId, bool approve, string? comment, CancellationToken ct = default);
    Task<ApprovalRequest> WithdrawAsync(Guid requestId, CancellationToken ct = default);
}

public interface IFileStore
{
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default);
    Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

public interface INotifier
{
    Task NotifyUserAsync(Guid userId, string title, string? body = null, string? link = null, string severity = "info", CancellationToken ct = default);
}

public interface IModuleAccess
{
    /// <summary>Drops the cached enabled-module set of the current workspace (call after enabling/disabling a module).</summary>
    void Invalidate();
    /// <summary>True when the module is core or enabled for the current workspace.</summary>
    Task<bool> IsEnabledAsync(string moduleKey, CancellationToken ct = default);
    Task<IReadOnlySet<string>> EnabledModulesAsync(CancellationToken ct = default);
}

/// <summary>A setting a module (or the core) declares. Values are stored per workspace, optionally per legal entity.</summary>
public sealed record SettingDefinition(string Key, string Label, string DefaultJson, string Type = "number", string? Description = null);

/// <summary>Every setting known to this deployment (core + installed modules). Implemented by the host from the module registry.</summary>
public interface ISettingsCatalog
{
    IReadOnlyList<(SettingDefinition Def, string Module)> Definitions();
}

public interface ISettingsService
{
    Task<string?> GetRawAsync(string key, Guid? legalEntityId = null, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key, Guid? legalEntityId = null, CancellationToken ct = default);
    Task SetRawAsync(string key, string jsonValue, Guid? legalEntityId = null, CancellationToken ct = default);
}

/// <summary>Pushes real-time events (implemented with SignalR in the API host).</summary>
public interface INotificationPusher { Task PushAsync(Guid userId, object payload, CancellationToken ct = default); }

/// <summary>User accounts (ASP.NET Identity lives behind this). Workspace membership and roles are ordinary repositories.</summary>
public interface IUserDirectory
{
    Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Members of the current workspace with their roles.</summary>
    Task<IReadOnlyList<UserSummary>> ListMembersAsync(CancellationToken ct = default);
    /// <summary>Finds the account by email or creates it with the temporary password (throws FieldValidationException when the password is rejected).</summary>
    Task<UserAccount> EnsureAccountAsync(string email, string fullName, string? temporaryPassword, CancellationToken ct = default);
}
