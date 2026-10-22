using System.Text.RegularExpressions;
using ERP.Application.Abstractions;
using ERP.Application.Templates;
using ERP.Domain.Common;
using ERP.Domain.Parties;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using ERP.Modularity;
using ERP.Modules.Inventory;
using ERP.Modules.RealEstate;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ERP.UnitTests;

internal sealed class TestOrgContext : IOrgContext
{
    public Guid? OrgId { get; set; }
    public Guid? LegalEntityId { get; set; }
    public IReadOnlyCollection<Guid>? AllowedLegalEntityIds { get; set; }
    public IDisposable Use(Guid orgId) { var prev = OrgId; OrgId = orgId; return new Restore(() => OrgId = prev); }
    public void SetLegalEntity(Guid? id) => LegalEntityId = id;
    private sealed class Restore(Action a) : IDisposable { public void Dispose() => a(); }
}

internal sealed class TestUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public Guid? UserId { get; } = Guid.NewGuid();
    public Guid? OrgId => null;
    public string? Email => "test@example.test";
    public Guid? LegalEntityId => null;
    public IReadOnlyCollection<Guid>? AllowedLegalEntityIds => null;
    public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    public string? IpAddress => "127.0.0.1";
    public bool HasPermission(string permission) => false;
}

public class ModelTests
{
    private static (AppDbContext Db, TestOrgContext Org, SqliteConnection Conn) Create(params IModule[] modules)
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var org = new TestOrgContext();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn)
            .ReplaceService<IModelCacheKeyFactory, ModuleAwareModelCacheKeyFactory>()
            .AddInterceptors(new AuditSaveChangesInterceptor(new TestUser(), org, new SystemClock())).Options;
        var db = new AppDbContext(options, new ModuleRegistry(modules), org);
        db.Database.EnsureCreated();
        return (db, org, conn);
    }

    [Fact]
    public void Every_org_scoped_entity_has_a_query_filter_on_OrgId()
    {
        var (db, _, conn) = Create(new RealEstateModule(), new InventoryModule());
        using var _c = conn;
        var scoped = db.Model.GetEntityTypes().Where(t => typeof(IOrgScoped).IsAssignableFrom(t.ClrType)).ToList();
        Assert.NotEmpty(scoped);
        foreach (var et in scoped)
        {
            var filter = et.GetQueryFilter();
            Assert.True(filter is not null && filter.ToString().Contains("OrgId"), $"{et.ClrType.Name} has no OrgId query filter");
        }
        Assert.Contains(db.Model.GetEntityTypes(), t => t.ClrType == typeof(Warehouse)); // module EF config applied
    }

    [Fact]
    public void Queries_only_see_the_current_workspace_and_fail_closed_without_one()
    {
        var (db, org, conn) = Create();
        using var _c = conn;
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        using (org.Use(a)) { db.Parties.Add(new Party { Code = "P1", Name = "A party", OrgId = a, Roles = PartyRoles.Customer }); db.SaveChanges(); }
        using (org.Use(b)) { db.Parties.Add(new Party { Code = "P1", Name = "B party", OrgId = b, Roles = PartyRoles.Customer }); db.SaveChanges(); }
        db.ChangeTracker.Clear();

        using (org.Use(a)) Assert.Equal(["A party"], db.Parties.Select(p => p.Name).ToList());
        using (org.Use(b)) Assert.Equal(["B party"], db.Parties.Select(p => p.Name).ToList());
        Assert.Empty(db.Parties.ToList()); // no workspace → no rows
    }

    [Fact]
    public void Writing_another_workspace_row_is_blocked_and_soft_delete_hides_rows()
    {
        var (db, org, conn) = Create();
        using var _c = conn;
        Guid a = Guid.NewGuid();
        using (org.Use(a))
        {
            db.Parties.Add(new Party { Code = "X", Name = "Other", OrgId = Guid.NewGuid(), Roles = PartyRoles.Vendor });
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            db.ChangeTracker.Clear();

            var p = new Party { Code = "Y", Name = "Mine", Roles = PartyRoles.Vendor };
            db.Parties.Add(p);
            db.SaveChanges();
            Assert.Equal(a, p.OrgId); // stamped from context
            db.Parties.Remove(p);
            db.SaveChanges();
            db.ChangeTracker.Clear();
            Assert.Empty(db.Parties.ToList());
            Assert.Single(db.Parties.IgnoreQueryFilters().Where(x => x.OrgId == a && x.IsDeleted).ToList());
            Assert.Contains(db.AuditEvents.ToList(), e => e.Action == "Deleted" || e.Action == "Updated");
        }
    }

    [Fact]
    public void Core_schema_and_core_seed_contain_no_industry_vocabulary()
    {
        var terms = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "core-vocabulary-denylist.txt"))
            .Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        var (db, _, conn) = Create(); // core only, no modules
        using var _c = conn;
        var names = new List<string>();
        foreach (var et in db.Model.GetEntityTypes())
        {
            names.Add(et.GetTableName() ?? "");
            names.Add(et.ClrType.Name);
            names.AddRange(et.GetProperties().Select(p => p.Name));
        }
        names.AddRange(CoreTemplates.ChartOfAccounts.Select(a => a.Name));
        names.AddRange(CoreTemplates.Roles.Select(r => r.Name));
        names.AddRange(CoreTemplates.Sequences.Select(s => s.Prefix + " " + s.DocumentType));
        names.AddRange(SettingsService.CoreSettings.Select(s => s.Key + " " + s.Label));

        var hits = new List<string>();
        foreach (var name in names)
        {
            // "Business unit" is the approved neutral core term (owner decision), so it is not an industry hit.
            var words = Regex.Replace(Regex.Replace(name, "([a-z])([A-Z])", "$1 $2"), "business ?units?", "bu", RegexOptions.IgnoreCase);
            foreach (var t in terms)
                if (Regex.IsMatch(words, $@"(^|[^A-Za-z]){Regex.Escape(t)}($|[^A-Za-z])", RegexOptions.IgnoreCase) )
                    hits.Add($"{name} ~ {t}");
        }
        Assert.True(hits.Count == 0, string.Join("; ", hits.Distinct()));
    }

    [Fact]
    public void Module_registry_rejects_duplicates_and_foreign_permission_prefixes()
    {
        Assert.Throws<InvalidOperationException>(() => new ModuleRegistry([new InventoryModule(), new InventoryModule()]));
        Assert.Throws<InvalidOperationException>(() => new ModuleRegistry([new BadModule()]));
        var reg = new ModuleRegistry([new RealEstateModule(), new InventoryModule()]);
        Assert.True(reg.IsInstalled("realestate"));
        Assert.Equal("inventory,realestate", reg.Signature);
    }

    [Fact]
    public void Permission_catalog_expands_wildcard_and_drops_unknown_keys()
    {
        var catalog = new PermissionCatalog(new ModuleRegistry([new InventoryModule()]));
        Assert.Contains("inventory.warehouses.read", catalog.Expand(["*"]));
        Assert.Equal(["core.users.read"], catalog.Expand(["core.users.read", "made.up.key"]));
    }

    [Fact]
    public void Sequential_guids_are_unique_and_time_ordered_for_sql_server()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => SequentialGuid.NewGuid()).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var sqlOrder = ids.OrderBy(g => new System.Data.SqlTypes.SqlGuid(g)).ToList();
        Assert.Equal(ids, sqlOrder);
    }

    private sealed class BadModule : ModuleBase
    {
        public override ModuleManifest Manifest { get; } = new("bad", "Bad", "", "Test");
        public override IEnumerable<ERP.Application.Security.PermissionDefinition> Permissions => [ERP.Application.Security.PermissionDefinition.Of("core.users.write", "steal")];
    }
}
