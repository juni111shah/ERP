using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.IndustryPacks.Domain;
using ERP.Modules.IndustryPacks.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.IndustryPacks;

/// <summary>
/// An industry module made of record types. Permissions, menu, numbering, list, form, status rules, billing, dashboard cards and search
/// all come from the definitions, so an industry adds business meaning without repeating plumbing.
/// </summary>
public abstract class PackModule : ModuleBase
{
    public abstract string Key { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual string Category => "Industry";
    public virtual IReadOnlyList<string>? Requires => null;
    public virtual int GroupOrder => 400;
    public abstract IReadOnlyList<RecordTypeDef> Types { get; }
    /// <summary>Operational roles offered with the module, besides the generated Manager and Viewer.</summary>
    protected virtual IEnumerable<RoleTemplate> ExtraRoles => [];

    public override ModuleManifest Manifest => new(Key, Name, Description, Category, Requires: Requires, RoutePrefix: "industry/" + Key);

    public static string ReadPermission(string module, string type) => $"{module}.{type}.read";
    public static string ManagePermission(string module, string type) => $"{module}.{type}.manage";
    public static string ApprovePermission(string module, string type) => $"{module}.{type}.approve";
    public static string SensitivePermission(string module, string type) => $"{module}.{type}.sensitive";
    public static string SequenceKey(string module, string type) => $"{module}.{type}";

    public override IEnumerable<PermissionDefinition> Permissions => Types.SelectMany(t =>
    {
        var list = new List<PermissionDefinition>
        {
            PermissionDefinition.Of(ReadPermission(Key, t.Key), $"View {t.Label.ToLowerInvariant()}"),
            PermissionDefinition.Of(ManagePermission(Key, t.Key), $"Create and change {t.Label.ToLowerInvariant()}"),
        };
        if (t.Transitions.Any(x => x.Approval)) list.Add(PermissionDefinition.Of(ApprovePermission(Key, t.Key), $"Approve {t.Label.ToLowerInvariant()}"));
        if (t.Sensitive || t.Fields.Any(f => f.Sensitive)) list.Add(PermissionDefinition.Of(SensitivePermission(Key, t.Key), $"See restricted details of {t.Label.ToLowerInvariant()}"));
        return list;
    });

    public override IEnumerable<MenuItemDefinition> Menu => Types.Select(t =>
        new MenuItemDefinition($"{Key}.{t.Key}", t.Label, $"/industry/{Key}/{t.Key}", t.Icon, ReadPermission(Key, t.Key), t.MenuOrder, Key, Name, GroupOrder));

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
        Types.Select(t => new CustomizableEntityDefinition($"{Key}.{t.Key}", t.Singular, typeof(IndustryRecord), Key, ReadPermission(Key, t.Key)));

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new($"{Key}-module", Name, Description, IndustryAvailability.Available, Requires is { } r ? [.. r, Key] : [Key],
            Roles: DefaultRoles().ToList(),
            Sequences: Types.Select(t => new SequenceTemplate(SequenceKey(Key, t.Key), t.Prefix)).ToList(),
            SortOrder: 800,
            Hidden: true),
    ];

    private IEnumerable<RoleTemplate> DefaultRoles()
    {
        yield return new($"{Name} manager", $"Runs {Name.ToLowerInvariant()} and approves its documents",
            Permissions.Where(p => !p.Key.EndsWith(".sensitive", StringComparison.Ordinal)).Select(p => p.Key).ToList(), Key);
        yield return new($"{Name} viewer", $"Reads {Name.ToLowerInvariant()}",
            Types.Select(t => ReadPermission(Key, t.Key)).ToList(), Key);
        foreach (var role in ExtraRoles) yield return role;
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        PackCatalog.Register(this);
        services.AddScoped<IDashboardContributor>(sp => ActivatorUtilities.CreateInstance<PackDashboard>(sp, this));
        services.AddScoped<ISearchProvider>(sp => ActivatorUtilities.CreateInstance<PackSearch>(sp, this));
    }

    public override void ConfigureModel(ModelBuilder mb) => PackModel.Configure(mb);

    public override void MapEndpoints(RouteGroupBuilder group) => PackEndpoints.Map(group, this);
}

/// <summary>The definitions of every installed pack, for the shared handlers.</summary>
public static class PackCatalog
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PackModule> Packs = new(StringComparer.Ordinal);

    public static void Register(PackModule pack) => Packs[pack.Key] = pack;

    public static PackModule Require(string module) => Packs.TryGetValue(module, out var pack)
        ? pack
        : throw new ERP.Application.Common.NotFoundException($"Unknown module '{module}'.");

    public static RecordTypeDef Require(string module, string type) => Require(module).Types.FirstOrDefault(t => t.Key == type)
        ?? throw new ERP.Application.Common.NotFoundException($"Unknown record type '{type}'.");
}

internal static class PackModel
{
    public static void Configure(ModelBuilder mb)
    {
        mb.Entity<IndustryRecord>(b =>
        {
            b.ToTable("ind_Records");
            b.Property(x => x.ModuleKey).HasMaxLength(40).IsRequired();
            b.Property(x => x.RecordType).HasMaxLength(40).IsRequired();
            b.Property(x => x.Number).HasMaxLength(50).IsRequired();
            b.Property(x => x.Title).HasMaxLength(300).IsRequired();
            b.Property(x => x.Status).HasMaxLength(40).IsRequired();
            b.Property(x => x.PartyName).HasMaxLength(300);
            b.Property(x => x.ParentTitle).HasMaxLength(300);
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.Notes).HasMaxLength(4000);
            b.HasIndex(x => new { x.OrgId, x.ModuleKey, x.RecordType, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.ModuleKey, x.RecordType, x.Status });
            b.HasIndex(x => new { x.OrgId, x.ParentId });
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.RecordId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.RecordId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<IndustryRecordLine>(b =>
        {
            b.ToTable("ind_RecordLines");
            b.Property(x => x.Code).HasMaxLength(50);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.UnitOfMeasure).HasMaxLength(30);
            b.Property(x => x.Quantity).HasPrecision(19, 4);
            b.Property(x => x.UnitPrice).HasPrecision(19, 4);
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.CompletedQuantity).HasPrecision(19, 4);
        });
        mb.Entity<IndustryRecordEvent>(b =>
        {
            b.ToTable("ind_RecordEvents");
            b.Property(x => x.FromStatus).HasMaxLength(40);
            b.Property(x => x.ToStatus).HasMaxLength(40).IsRequired();
            b.Property(x => x.UserName).HasMaxLength(200);
            b.Property(x => x.Reason).HasMaxLength(1000);
        });
    }
}
