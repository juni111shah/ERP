using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using ERP.Modularity;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Assets;

/// <summary>Fixed assets: categories, the register, capitalisation, monthly depreciation, disposal and their reports. Everything reaches the ledger through the posting service.</summary>
public sealed class AssetsModule : ModuleBase
{
    public const string Key = "assets";
    public const string AssetSequence = "fixedasset";

    public override ModuleManifest Manifest { get; } = new(Key, "Fixed Assets", "Asset register, depreciation runs, disposals and net book value.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(Assets.Permissions.CategoriesRead, "View asset categories"),
        PermissionDefinition.Of(Assets.Permissions.CategoriesManage, "Create and edit asset categories and their accounts"),
        PermissionDefinition.Of(Assets.Permissions.RegisterRead, "View the asset register"),
        PermissionDefinition.Of(Assets.Permissions.RegisterManage, "Add and edit assets in the register"),
        PermissionDefinition.Of(Assets.Permissions.RegisterCapitalise, "Capitalise assets (posts to the ledger)"),
        PermissionDefinition.Of(Assets.Permissions.RegisterDispose, "Dispose of assets (posts to the ledger)"),
        PermissionDefinition.Of(Assets.Permissions.DepreciationRead, "View depreciation runs"),
        PermissionDefinition.Of(Assets.Permissions.DepreciationRun, "Run monthly depreciation (posts to the ledger)"),
        PermissionDefinition.Of(Assets.Permissions.ReportsRead, "View asset register and depreciation schedule reports"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("assets.register", "Asset register", "/assets/register", "database", Assets.Permissions.RegisterRead, 10, "assets", "Fixed assets", 340),
        new("assets.categories", "Asset categories", "/assets/categories", "appstore", Assets.Permissions.CategoriesRead, 20, "assets", "Fixed assets", 340),
        new("assets.depreciation", "Depreciation", "/assets/depreciation", "fall", Assets.Permissions.DepreciationRead, 30, "assets", "Fixed assets", 340),
        new("assets.reports", "Asset reports", "/assets/reports", "file-text", Assets.Permissions.ReportsRead, 40, "assets", "Fixed assets", 340),
    ];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("asset_intensive", "Asset-Intensive Business", "Equipment, vehicles or machinery tracked through a register with monthly depreciation.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Asset accountant", "Runs the asset register, depreciation and disposals",
                    [Assets.Permissions.CategoriesRead, Assets.Permissions.CategoriesManage, Assets.Permissions.RegisterRead, Assets.Permissions.RegisterManage, Assets.Permissions.RegisterCapitalise,
                     Assets.Permissions.RegisterDispose, Assets.Permissions.DepreciationRead, Assets.Permissions.DepreciationRun, Assets.Permissions.ReportsRead, CorePermissions.AccountsRead], Key),
                new("Asset viewer", "Reads the register and its reports",
                    [Assets.Permissions.CategoriesRead, Assets.Permissions.RegisterRead, Assets.Permissions.DepreciationRead, Assets.Permissions.ReportsRead], Key),
            ],
            Sequences:
            [
                new(AssetSequence, "FA", "{prefix}-{seq:5}", false),
            ],
            SortOrder: 45),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDashboardContributor, AssetsDashboard>();
        services.AddScoped<FixedAssetChecks>();
        services.AddScoped<AssetCategoryRules>();
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<AssetCategory>(b =>
        {
            b.ToTable("ast_Categories");
            b.Property(x => x.Code).HasMaxLength(30).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.DecliningRatePercent).HasPrecision(7, 4);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AssetAccountId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccumulatedDepreciationAccountId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.DepreciationExpenseAccountId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Account>().WithMany().HasForeignKey(x => x.DisposalGainLossAccountId).OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<FixedAsset>(b =>
        {
            b.ToTable("ast_Assets");
            b.Property(x => x.Number).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Custodian).HasMaxLength(200);
            b.Property(x => x.DisposalReason).HasMaxLength(400);
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.CapitalisationSource).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.DecliningRatePercent).HasPrecision(7, 4);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status });
            b.HasIndex(x => x.SourceBillLineId);
            b.HasOne<AssetCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<DepreciationRun>(b =>
        {
            b.ToTable("ast_DepreciationRuns");
            // At most one run per period, also when two arrive at once.
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Year, x.Month }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<DepreciationEntry>(b =>
        {
            b.ToTable("ast_DepreciationEntries");
            b.HasIndex(x => new { x.AssetId, x.PeriodEnd });
            b.HasIndex(x => x.RunId);
            b.HasOne<FixedAsset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<DepreciationRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
