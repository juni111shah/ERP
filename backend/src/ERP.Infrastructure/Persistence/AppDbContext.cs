using System.Linq.Expressions;
using ERP.Application.Abstractions;
using ERP.Domain.Catalog;
using ERP.Domain.Common;
using ERP.Domain.Customization;
using ERP.Domain.Finance;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using ERP.Infrastructure.Identity;
using ERP.Modularity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ERP.Infrastructure.Persistence;

public class AppDbContext : IdentityUserContext<AppUser, Guid>, IAppDbContext, IUnitOfWork
{
    private readonly IOrgContext? _org;
    private readonly ModuleRegistry _modules;

    public AppDbContext(DbContextOptions<AppDbContext> options, ModuleRegistry modules, IOrgContext? org = null) : base(options)
    {
        _modules = modules;
        _org = org;
    }

    public ModuleRegistry ModuleRegistry => _modules;

    // Values read by the global query filters (parameterised per query). Null org → Guid.Empty → no rows (fail closed).
    public Guid CurrentOrgId => _org?.OrgId ?? Guid.Empty;
    public Guid? CurrentLegalEntityId => _org?.LegalEntityId;
    public bool AllLegalEntities => _org?.AllowedLegalEntityIds is null;
    public List<Guid> AllowedLegalEntityIds => _org?.AllowedLegalEntityIds?.ToList() ?? [];

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<OrganizationModule> OrganizationModules => Set<OrganizationModule>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public new DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserLegalEntityAccess> UserLegalEntityAccess => Set<UserLegalEntityAccess>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<TaxCode> TaxCodes => Set<TaxCode>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<PaymentInstrument> PaymentInstruments => Set<PaymentInstrument>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PortalOtp> PortalOtps => Set<PortalOtp>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(18, 2);
        b.Properties<string>().HaveMaxLength(400);
        foreach (var enumType in new[] { typeof(AccountType), typeof(JournalStatus), typeof(TaxKind), typeof(PartyKind), typeof(ItemType),
                     typeof(ApprovalStatus), typeof(CustomFieldDataType), typeof(InstrumentKind), typeof(InstrumentDirection), typeof(InstrumentStatus), typeof(DocumentStatus), typeof(SettlementMethod), typeof(StatementLineStatus) })
            b.Properties(enumType).HaveConversion<string>().HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
        var sqlServer = Database.IsSqlServer();

        mb.Entity<AppUser>(b =>
        {
            b.ToTable("Users");
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.HasIndex(x => x.NormalizedEmail).IsUnique();
        });
        mb.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("UserClaims");
        mb.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("UserLogins");
        mb.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("UserTokens");

        mb.Entity<Organization>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            b.Property(x => x.BaseCurrency).HasMaxLength(3).IsFixedLength();
        });
        mb.Entity<LegalEntity>(b =>
        {
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            b.Property(x => x.BaseCurrency).HasMaxLength(3).IsFixedLength();
        });
        mb.Entity<BusinessUnit>(b => b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Code }).IsUnique());
        mb.Entity<OrganizationModule>(b => b.HasIndex(x => new { x.OrgId, x.ModuleKey }).IsUnique());
        mb.Entity<OrganizationMember>(b => b.HasIndex(x => new { x.OrgId, x.UserId }).IsUnique());
        mb.Entity<Role>(b =>
        {
            b.ToTable("AppRoles");
            b.HasIndex(x => new { x.OrgId, x.Name }).IsUnique();
            b.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<RolePermission>(b => { b.HasIndex(x => new { x.RoleId, x.Permission }).IsUnique(); b.Property(x => x.Permission).HasMaxLength(100); });
        mb.Entity<UserRole>(b =>
        {
            b.ToTable("AppUserRoles");
            b.HasIndex(x => new { x.OrgId, x.UserId, x.RoleId }).IsUnique();
            b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<UserLegalEntityAccess>(b => b.HasIndex(x => new { x.OrgId, x.UserId, x.LegalEntityId }).IsUnique());
        mb.Entity<RefreshToken>(b => { b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => x.FamilyId); b.Property(x => x.TokenHash).HasMaxLength(100); });
        mb.Entity<PasswordResetToken>(b => { b.HasIndex(x => x.TokenHash).IsUnique(); b.Property(x => x.TokenHash).HasMaxLength(100); });
        mb.Entity<Party>(b =>
        {
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.OrgId, x.Name });
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
        mb.Entity<Item>(b => b.HasIndex(x => new { x.OrgId, x.Sku }).IsUnique());
        mb.Entity<Currency>(b => { b.HasIndex(x => x.Code).IsUnique(); b.Property(x => x.Code).HasMaxLength(3).IsFixedLength(); });
        mb.Entity<ExchangeRate>(b =>
        {
            b.HasIndex(x => new { x.OrgId, x.FromCurrency, x.ToCurrency, x.RateDate }).IsUnique();
            b.Property(x => x.Rate).HasPrecision(18, 8);
        });
        mb.Entity<TaxCode>(b => { b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Code }).IsUnique(); b.Property(x => x.Rate).HasPrecision(9, 4); });
        mb.Entity<Account>(b => { b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); b.Property(x => x.Code).HasMaxLength(20); });
        mb.Entity<JournalEntry>(b =>
        {
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique();
            b.Property(x => x.ExchangeRate).HasPrecision(18, 8);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
            b.Ignore(x => x.TotalDebit); b.Ignore(x => x.TotalCredit);
        });
        mb.Entity<JournalLine>(b => b.HasIndex(x => new { x.AccountId, x.JournalEntryId }));
        mb.Entity<Budget>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.FiscalYear });
        });
        mb.Entity<BudgetLine>(b => { b.Property(x => x.Amount).HasPrecision(19, 4); b.HasIndex(x => new { x.BudgetId, x.AccountId, x.Month }).IsUnique(); });
        mb.Entity<PaymentInstrument>(b => b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique());
        mb.Entity<Invoice>(b =>
        {
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique();
            b.Property(x => x.ExchangeRate).HasPrecision(18, 8);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<InvoiceLine>(b => b.Property(x => x.Quantity).HasPrecision(18, 4));
        mb.Entity<Bill>(b =>
        {
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.OrgId, x.PartyId, x.VendorReference });
            b.Property(x => x.ExchangeRate).HasPrecision(18, 8);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<BillLine>(b => b.Property(x => x.Quantity).HasPrecision(18, 4));
        mb.Entity<Settlement>(b =>
        {
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique();
            b.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<BankStatementLine>(b => { b.HasIndex(x => new { x.OrgId, x.AccountId, x.Date }); b.HasIndex(x => x.MatchedJournalLineId); });
        mb.Entity<NumberSequence>(b => b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.DocumentType, x.Year }).IsUnique());
        mb.Entity<ApprovalRequest>(b => b.HasIndex(x => new { x.OrgId, x.Status }));
        mb.Entity<Attachment>(b => { b.HasIndex(x => new { x.OrgId, x.OwnerType, x.OwnerId }); b.HasIndex(x => new { x.OrgId, x.ExpiresOn }); b.Property(x => x.Category).HasMaxLength(50); b.Property(x => x.Description).HasMaxLength(500); });
        mb.Entity<AuditEvent>(b =>
        {
            b.HasIndex(x => new { x.OrgId, x.RecordType, x.RecordId });
            b.HasIndex(x => new { x.OrgId, x.OccurredAt });
            b.Property(x => x.Before).Metadata.SetMaxLength(null);
            b.Property(x => x.After).Metadata.SetMaxLength(null);
            b.Property(x => x.ChangedFields).HasMaxLength(4000);
        });
        mb.Entity<Notification>(b => b.HasIndex(x => new { x.OrgId, x.UserId, x.ReadAt }));
        mb.Entity<PortalOtp>(b => { b.HasIndex(x => new { x.OrgId, x.PartyId }); b.Property(x => x.CodeHash).HasMaxLength(100); });
        mb.Entity<MessageLog>(b => { b.HasIndex(x => new { x.OrgId, x.DedupeKey }).IsUnique(); b.Property(x => x.DedupeKey).HasMaxLength(300); b.Property(x => x.Body).HasMaxLength(2000); b.Property(x => x.Channel).HasMaxLength(30); });
        mb.Entity<Setting>(b => { b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Key }).IsUnique(); b.Property(x => x.Value).HasMaxLength(4000); });
        mb.Entity<CustomFieldDefinition>(b =>
        {
            b.HasIndex(x => new { x.OrgId, x.EntityType, x.Key }).IsUnique();
            b.Property(x => x.Key).HasMaxLength(64).IsRequired();
            b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            b.Property(x => x.Label).HasMaxLength(100).IsRequired();
            b.Property(x => x.Options).Metadata.SetMaxLength(null);
            if (sqlServer) b.ToTable(t => t.HasCheckConstraint("CK_CustomFieldDefinitions_Options_IsJson", "[Options] IS NULL OR ISJSON([Options]) = 1"));
        });

        foreach (var module in _modules.Modules) module.ConfigureModel(mb);

        ApplyCrossCuttingConventions(mb, sqlServer);
    }

    /// <summary>Runs the work in one transaction through the provider's execution strategy (safe with SQL Server retry-on-failure).</summary>
    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is not null) return await work(ct); // already inside one (e.g. onboarding): join it
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(ct);
            var result = await work(ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    /// <summary>Global query filters, rowversion and the custom-fields JSON column for every entity (core and modules).</summary>
    private void ApplyCrossCuttingConventions(ModelBuilder mb, bool sqlServer)
    {
        foreach (var et in mb.Model.GetEntityTypes().Where(t => t.BaseType is null && !t.IsOwned()).ToList())
        {
            var clr = et.ClrType;
            var builder = mb.Entity(clr);

            if (typeof(OrgEntity).IsAssignableFrom(clr))
            {
                if (sqlServer) builder.Property(nameof(OrgEntity.RowVersion)).IsRowVersion();
                else builder.Ignore(nameof(OrgEntity.RowVersion));
            }

            if (typeof(IHasCustomFields).IsAssignableFrom(clr))
            {
                builder.Property(nameof(IHasCustomFields.CustomFields)).Metadata.SetMaxLength(null);
                if (sqlServer)
                    builder.ToTable(t => t.HasCheckConstraint($"CK_{et.GetTableName()}_CustomFields_IsJson", "[CustomFields] IS NULL OR ISJSON([CustomFields]) = 1"));
            }

            var filter = BuildFilter(clr);
            if (filter is not null) builder.HasQueryFilter(filter);
        }
    }

    private LambdaExpression? BuildFilter(Type clr)
    {
        var p = Expression.Parameter(clr, "e");
        var ctx = Expression.Constant(this);
        Expression? body = null;

        if (typeof(IOrgScoped).IsAssignableFrom(clr))
            body = Expression.Equal(Expression.Property(p, nameof(IOrgScoped.OrgId)), Expression.Property(ctx, nameof(CurrentOrgId)));

        if (typeof(ILegalEntityScoped).IsAssignableFrom(clr))
        {
            var le = Expression.Property(p, nameof(ILegalEntityScoped.LegalEntityId));
            // AllLegalEntities || AllowedLegalEntityIds.Contains(e.LegalEntityId)
            var allowed = Expression.OrElse(
                Expression.Property(ctx, nameof(AllLegalEntities)),
                Expression.Call(Expression.Property(ctx, nameof(AllowedLegalEntityIds)), typeof(List<Guid>).GetMethod(nameof(List<Guid>.Contains))!, le));
            // CurrentLegalEntityId == null || e.LegalEntityId == CurrentLegalEntityId
            var current = Expression.Property(ctx, nameof(CurrentLegalEntityId));
            var selected = Expression.OrElse(
                Expression.Equal(current, Expression.Constant(null, typeof(Guid?))),
                Expression.Equal(Expression.Convert(le, typeof(Guid?)), current));
            var leFilter = Expression.AndAlso(allowed, selected);
            body = body is null ? leFilter : Expression.AndAlso(body, leFilter);
        }

        if (typeof(ISoftDelete).IsAssignableFrom(clr))
        {
            var notDeleted = Expression.Not(Expression.Property(p, nameof(ISoftDelete.IsDeleted)));
            body = body is null ? notDeleted : Expression.AndAlso(body, notDeleted);
        }

        return body is null ? null : Expression.Lambda(body, p);
    }
}

/// <summary>The model depends on the installed modules, so cache one model per module set.</summary>
public sealed class ModuleAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is AppDbContext app ? (context.GetType(), app.ModuleRegistry.Signature, context.Database.ProviderName, designTime) : (object)(context.GetType(), designTime);
}
