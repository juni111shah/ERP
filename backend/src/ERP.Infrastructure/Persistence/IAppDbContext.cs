using ERP.Application.Abstractions;
using ERP.Domain.Catalog;
using ERP.Domain.Customization;
using ERP.Domain.Finance;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

/// <summary>The EF Core context as seen by infrastructure services. Application code never sees it: it goes through repositories.</summary>
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
