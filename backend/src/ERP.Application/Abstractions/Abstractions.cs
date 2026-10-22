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

public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<LegalEntity> LegalEntities { get; }
    DbSet<BusinessUnit> BusinessUnits { get; }
    DbSet<OrganizationModule> OrganizationModules { get; }
    DbSet<OrganizationMember> OrganizationMembers { get; }
    DbSet<Role> Roles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<UserLegalEntityAccess> UserLegalEntityAccess { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<Party> Parties { get; }
    DbSet<Item> Items { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<TaxCode> TaxCodes { get; }
    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<PaymentInstrument> PaymentInstruments { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Bill> Bills { get; }
    DbSet<Settlement> Settlements { get; }
    DbSet<NumberSequence> NumberSequences { get; }
    DbSet<ApprovalPolicy> ApprovalPolicies { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Setting> Settings { get; }
    DbSet<CustomFieldDefinition> CustomFieldDefinitions { get; }
    DbSet<T> Set<T>() where T : class;
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> Entry<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

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
    /// <summary>True when the module is core or enabled for the current workspace.</summary>
    Task<bool> IsEnabledAsync(string moduleKey, CancellationToken ct = default);
    Task<IReadOnlySet<string>> EnabledModulesAsync(CancellationToken ct = default);
}

public interface ISettingsService
{
    Task<string?> GetRawAsync(string key, Guid? legalEntityId = null, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key, Guid? legalEntityId = null, CancellationToken ct = default);
    Task SetRawAsync(string key, string jsonValue, Guid? legalEntityId = null, CancellationToken ct = default);
}
