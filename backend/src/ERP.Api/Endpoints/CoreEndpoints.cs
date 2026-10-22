using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Customization;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Infrastructure.Auth;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Api.Endpoints;

public static class CoreEndpoints
{
    public sealed record LegalEntityInput(string Name, string? LegalName, string CountryCode, string BaseCurrency, string? TaxRegistrationNo, bool IsTaxRegistered, string? RegistrationNo, string? Address, string? Email, string? Phone, bool IsActive = true, JsonObject? CustomFields = null);
    public sealed record BusinessUnitInput(Guid LegalEntityId, string Code, string Name, string? Address, bool IsActive = true, JsonObject? CustomFields = null);
    public sealed record ItemInput(string Sku, string Name, ItemType Type, string Uom, decimal? SalesPrice, decimal? PurchasePrice, Guid? TaxCodeId, bool IsActive = true, JsonObject? CustomFields = null);
    public sealed record RoleInput(string Name, string? Description, string[] Permissions);
    public sealed record InviteUserInput(string Email, string FullName, string? TemporaryPassword, Guid[] RoleIds, Guid[]? LegalEntityIds);
    public sealed record ApprovalInput(string Subject, string? Justification, decimal? Amount, Guid? LegalEntityId);
    public sealed record DecisionInput(string? Comment);
    public sealed record ModuleView(string Key, string Name, string Description, string Category, string Version, bool IsCore, IReadOnlyList<string> Requires, string UiRoute, string State);

    public static void MapCore(this RouteGroupBuilder api)
    {
        MapMe(api);
        MapPlatform(api);
        MapModules(api);
        MapOrganization(api);
        MapSecurity(api);
        MapMasterData(api);
        MapFinance(api);
        MapCustomFields(api);
        MapWorkflow(api);
    }

    // ---- /me and navigation --------------------------------------------------
    private static void MapMe(RouteGroupBuilder api)
    {
        api.MapGet("/me", async (ICurrentUser user, AuthService auth, AppDbContext db, ModuleRegistry modules, CancellationToken ct) =>
        {
            var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == user.UserId, ct);
            var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == user.OrgId, ct);
            var (perms, ents, enabled) = await auth.ResolveAccessAsync(u.Id, org.Id, ct);
            var entities = await db.LegalEntities.AsNoTracking()
                .Where(e => ents == null || ents.Contains(e.Id)).OrderBy(e => e.Code)
                .Select(e => new { e.Id, e.Code, e.Name, e.BaseCurrency }).ToListAsync(ct);
            return Results.Ok(new
            {
                user = new { u.Id, u.Email, u.FullName, u.MustChangePassword },
                workspace = new { org.Id, org.Code, org.Name, org.IndustryCode, org.CountryCode, org.BaseCurrency, org.TimeZone, org.DefaultLocale },
                workspaces = await auth.WorkspacesForAsync(u.Id, ct),
                permissions = perms.OrderBy(p => p),
                legalEntities = entities,
                allLegalEntities = ents is null,
                enabledModules = modules.Modules.Where(m => m.Manifest.IsCore || enabled.Contains(m.Manifest.Key)).Select(m => new { m.Manifest.Key, m.Manifest.UiRoute }),
            });
        }).RequireAuthorization().WithTags("Session");

        api.MapGet("/navigation/menu", async (ICurrentUser user, IModuleAccess access, ModuleRegistry modules, CancellationToken ct) =>
        {
            var enabled = await access.EnabledModulesAsync(ct);
            var items = CoreMenu.Items.Select(i => (Item: i, Module: "core"))
                .Concat(modules.Modules.Where(m => m.Manifest.IsCore || enabled.Contains(m.Manifest.Key)).SelectMany(m => m.Menu.Select(i => (Item: i, Module: m.Manifest.Key))))
                .Where(x => x.Item.Permission is null || user.HasPermission(x.Item.Permission));
            var groups = items.GroupBy(x => x.Item.GroupId)
                .Select(g => new
                {
                    id = g.Key, label = g.First().Item.GroupLabel, order = g.First().Item.GroupOrder,
                    items = g.OrderBy(x => x.Item.Order).Select(x => new { x.Item.Id, x.Item.Label, x.Item.Route, x.Item.Icon, x.Item.Permission, module = x.Module }),
                })
                .OrderBy(g => g.order);
            return Results.Ok(groups);
        }).RequireAuthorization().WithTags("Session");
    }

    // ---- Platform: industries and onboarding ---------------------------------
    private static void MapPlatform(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/platform").WithTags("Platform");
        g.MapGet("/industries", (OnboardingService s) => Results.Ok(s.IndustryViews())).AllowAnonymous();
        g.MapPost("/onboarding", async (OnboardingRequest r, OnboardingService s, IConfiguration config, CancellationToken ct) =>
        {
            if (!config.GetValue<bool>("Onboarding:AllowSelfSignup")) return Results.Problem(statusCode: 403, title: "Self sign-up is disabled");
            var result = await s.CreateWorkspaceAsync(r, ct);
            return Results.Created($"/api/v1/platform/workspaces/{result.OrgId}", result);
        }).AllowAnonymous().RequireRateLimiting("login");
    }

    // ---- Modules ----------------------------------------------------------------
    private static void MapModules(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/modules").WithTags("Modules");
        g.MapGet("", async (IModuleAccess access, ModuleRegistry modules, CancellationToken ct) =>
        {
            var enabled = await access.EnabledModulesAsync(ct);
            var installed = modules.Modules.Select(m => new ModuleView(m.Manifest.Key, m.Manifest.Name, m.Manifest.Description, m.Manifest.Category,
                m.Manifest.Version, m.Manifest.IsCore, m.Manifest.Requires ?? [], m.Manifest.UiRoute,
                m.Manifest.IsCore || enabled.Contains(m.Manifest.Key) ? "enabled" : "disabled"));
            var planned = ModuleRegistry.Planned.Where(p => !modules.IsInstalled(p.Key)).Select(p => new ModuleView(p.Key, p.Name, p.Description, p.Category,
                p.Version, p.IsCore, p.Requires ?? [], p.UiRoute, "unavailable"));
            return Results.Ok(installed.Concat(planned));
        }).RequirePermission(CorePermissions.SettingsRead);

        g.MapPost("/{key}/{action:regex(^(enable|disable)$)}", async (string key, string action, AppDbContext db, ModuleRegistry modules, IModuleAccess access, IOrgContext org, IMemoryCache cache, CancellationToken ct) =>
        {
            var m = modules.Find(key);
            if (m is null) return Results.Problem(statusCode: 404, title: "Module not installed");
            if (m.Manifest.IsCore) return Results.Problem(statusCode: 409, title: "Core modules cannot be disabled");
            var enable = action == "enable";
            var enabled = await access.EnabledModulesAsync(ct);
            if (enable && (m.Manifest.Requires ?? []).FirstOrDefault(r => !enabled.Contains(r)) is { } missing)
                return Results.Problem(statusCode: 409, title: $"Enable '{missing}' first");
            if (!enable && modules.Modules.Where(x => enabled.Contains(x.Manifest.Key) && (x.Manifest.Requires ?? []).Contains(key)).Select(x => x.Manifest.Key).ToList() is { Count: > 0 } dependents)
                return Results.Problem(statusCode: 409, title: $"Disable {string.Join(", ", dependents)} first");
            var row = await db.OrganizationModules.FirstOrDefaultAsync(x => x.ModuleKey == key, ct);
            if (row is null) { row = new OrganizationModule { ModuleKey = key }; db.OrganizationModules.Add(row); }
            if (row.IsEnabled != enable)
            {
                row.IsEnabled = enable;
                if (enable) row.EnabledAt = DateTime.UtcNow; else row.DisabledAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct); // module data is never touched
            }
            cache.Remove(ModuleAccess.CacheKey(org.OrgId!.Value));
            return Results.Ok(new { key, state = enable ? "enabled" : "disabled" });
        }).RequirePermission(CorePermissions.ModulesManage);
    }

    // ---- Organization: legal entities, business units, settings ---------------
    private static void MapOrganization(RouteGroupBuilder api)
    {
        var le = api.MapGroup("/legal-entities").WithTags("Organization");
        le.MapGet("", async (AppDbContext db, ICustomFieldService cf, CancellationToken ct) =>
        {
            var defs = await cf.GetDefinitionsAsync("legalentity", ct);
            var rows = await db.LegalEntities.AsNoTracking().OrderBy(e => e.Code).ToListAsync(ct);
            return rows.Select(e => LeDto(e, CustomFieldValidator.Project(e.CustomFields, defs)));
        }).RequirePermission(CorePermissions.EntitiesRead);
        le.MapPost("", async (LegalEntityInput i, AppDbContext db, INumberingService numbers, ICustomFieldService cf, CancellationToken ct) =>
        {
            var e = new LegalEntity { Code = await numbers.NextAsync("legalentity", ct: ct) };
            MapLe(i, e);
            await cf.ApplyAsync("legalentity", e, i.CustomFields, true, ct);
            db.LegalEntities.Add(e);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/legal-entities/{e.Id}", LeDto(e, await cf.ReadAsync("legalentity", e, ct)));
        }).RequirePermission(CorePermissions.EntitiesWrite);
        le.MapPut("/{id:guid}", async (Guid id, LegalEntityInput i, AppDbContext db, ICustomFieldService cf, CancellationToken ct) =>
        {
            var e = await db.LegalEntities.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
            MapLe(i, e);
            await cf.ApplyAsync("legalentity", e, i.CustomFields, false, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(LeDto(e, await cf.ReadAsync("legalentity", e, ct)));
        }).RequirePermission(CorePermissions.EntitiesWrite);

        var bu = api.MapGroup("/business-units").WithTags("Organization");
        bu.MapGet("", async (AppDbContext db, CancellationToken ct) =>
            await db.BusinessUnits.AsNoTracking().OrderBy(b => b.Code).Select(b => new { b.Id, b.LegalEntityId, b.Code, b.Name, b.Address, b.IsActive }).ToListAsync(ct))
            .RequirePermission(CorePermissions.BusinessUnitsRead);
        bu.MapPost("", async (BusinessUnitInput i, AppDbContext db, ICurrentUser user, ICustomFieldService cf, CancellationToken ct) =>
        {
            if (!await db.LegalEntities.AnyAsync(e => e.Id == i.LegalEntityId, ct) || (user.AllowedLegalEntityIds is { } a && !a.Contains(i.LegalEntityId)))
                throw new FieldValidationException("legalEntityId", "Legal entity not found.");
            if (string.IsNullOrWhiteSpace(i.Code) || string.IsNullOrWhiteSpace(i.Name)) throw new FieldValidationException("code", "Code and name are required.");
            var b = new BusinessUnit { LegalEntityId = i.LegalEntityId, Code = i.Code.Trim(), Name = i.Name.Trim(), Address = i.Address, IsActive = i.IsActive };
            await cf.ApplyAsync("businessunit", b, i.CustomFields, true, ct);
            db.BusinessUnits.Add(b);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/business-units/{b.Id}", new { b.Id, b.LegalEntityId, b.Code, b.Name });
        }).RequirePermission(CorePermissions.BusinessUnitsWrite);

        var st = api.MapGroup("/settings").WithTags("Organization");
        st.MapGet("", async (SettingsService settings, IModuleAccess access, CancellationToken ct) =>
        {
            var enabled = await access.EnabledModulesAsync(ct);
            var list = new List<object>();
            foreach (var (def, module) in settings.Definitions().Where(d => d.Module == "core" || enabled.Contains(d.Module)))
                list.Add(new { def.Key, def.Label, module, def.Type, def.Description, value = JsonDocument.Parse(await settings.GetRawAsync(def.Key, ct: ct) ?? "null").RootElement });
            return Results.Ok(list);
        }).RequirePermission(CorePermissions.SettingsRead);
        st.MapPut("", async (Dictionary<string, JsonElement> values, SettingsService settings, ICurrentUser user, IModuleAccess access, AppDbContext db, CancellationToken ct) =>
        {
            foreach (var (key, value) in values)
            {
                var module = key.Split('.')[0];
                if (module != "core")
                {
                    if (!await access.IsEnabledAsync(module, ct)) throw new FieldValidationException(key, "That module is not enabled.");
                    if (!user.HasPermission(CorePermissions.SettingsWrite) && !user.HasPermission($"{module}.settings.write")) throw new ForbiddenException();
                }
                else if (!user.HasPermission(CorePermissions.SettingsWrite)) throw new ForbiddenException();
                await settings.SetRawAsync(key, value.GetRawText(), ct: ct);
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    // ---- Users, roles, permissions, audit ------------------------------------
    private static void MapSecurity(RouteGroupBuilder api)
    {
        api.MapGet("/permissions", (IPermissionCatalog catalog) => catalog.All).RequirePermission(CorePermissions.RolesRead).WithTags("Security");

        var roles = api.MapGroup("/roles").WithTags("Security");
        roles.MapGet("", async (AppDbContext db, CancellationToken ct) =>
            await db.Roles.AsNoTracking().OrderBy(r => r.Name)
                .Select(r => new { r.Id, r.Name, r.Description, r.IsBuiltIn, r.ModuleKey, permissions = r.Permissions.Select(p => p.Permission).OrderBy(p => p) }).ToListAsync(ct))
            .RequirePermission(CorePermissions.RolesRead);
        roles.MapPost("", async (RoleInput i, AppDbContext db, IPermissionCatalog catalog, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(i.Name)) throw new FieldValidationException("name", "Name is required.");
            var unknown = i.Permissions.Where(p => p != CorePermissions.All && catalog.All.All(d => d.Key != p)).ToList();
            if (unknown.Count > 0) throw new FieldValidationException("permissions", $"Unknown permissions: {string.Join(", ", unknown)}");
            if (await db.Roles.AnyAsync(r => r.Name == i.Name, ct)) throw new ConflictException("A role with that name exists.");
            var role = new Role { Name = i.Name.Trim(), Description = i.Description };
            role.Permissions.AddRange(i.Permissions.Distinct().Select(p => new RolePermission { Permission = p }));
            db.Roles.Add(role);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/roles/{role.Id}", new { role.Id, role.Name });
        }).RequirePermission(CorePermissions.RolesWrite);

        var users = api.MapGroup("/users").WithTags("Security");
        users.MapGet("", async (AppDbContext db, CancellationToken ct) =>
        {
            var members = await db.OrganizationMembers.AsNoTracking().ToListAsync(ct);
            var ids = members.Select(m => m.UserId).ToList();
            var accounts = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(ct);
            var userRoles = await db.UserRoles.AsNoTracking().Include(r => r.Role).ToListAsync(ct);
            return accounts.OrderBy(a => a.FullName).Select(a => new
            {
                a.Id, a.Email, a.FullName, isActive = a.IsActive && members.First(m => m.UserId == a.Id).IsActive, a.LastSignInAt,
                roles = userRoles.Where(r => r.UserId == a.Id).Select(r => r.Role!.Name),
            });
        }).RequirePermission(CorePermissions.UsersRead);
        users.MapPost("", async (InviteUserInput i, AppDbContext db, UserManager<AppUser> um, CancellationToken ct) =>
        {
            var user = await um.FindByEmailAsync(i.Email.Trim());
            if (user is null)
            {
                if (string.IsNullOrWhiteSpace(i.TemporaryPassword)) throw new FieldValidationException("temporaryPassword", "A temporary password is required for new accounts.");
                user = new AppUser { Id = ERP.Domain.Common.SequentialGuid.NewGuid(), Email = i.Email.Trim(), UserName = i.Email.Trim(), FullName = i.FullName.Trim(), CreatedAt = DateTime.UtcNow, MustChangePassword = true };
                var r = await um.CreateAsync(user, i.TemporaryPassword);
                if (!r.Succeeded) throw new FieldValidationException(new Dictionary<string, string[]> { ["temporaryPassword"] = r.Errors.Select(e => e.Description).ToArray() });
            }
            if (await db.OrganizationMembers.AnyAsync(m => m.UserId == user.Id, ct)) throw new ConflictException("That user is already a member of this workspace.");
            var roleIds = await db.Roles.Where(r => i.RoleIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct); // roles of this workspace only
            db.OrganizationMembers.Add(new OrganizationMember { UserId = user.Id, JoinedAt = DateTime.UtcNow });
            db.UserRoles.AddRange(roleIds.Select(r => new UserRole { UserId = user.Id, RoleId = r }));
            if (i.LegalEntityIds is { Length: > 0 })
            {
                var ents = await db.LegalEntities.Where(e => i.LegalEntityIds.Contains(e.Id)).Select(e => e.Id).ToListAsync(ct);
                db.UserLegalEntityAccess.AddRange(ents.Select(e => new UserLegalEntityAccess { UserId = user.Id, LegalEntityId = e }));
            }
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/users/{user.Id}", new { user.Id, user.Email, user.FullName });
        }).RequirePermission(CorePermissions.UsersWrite);

        api.MapGet("/audit", async (string? recordType, string? recordId, int? take, AppDbContext db, CancellationToken ct) =>
        {
            var q = db.AuditEvents.AsNoTracking();
            if (!string.IsNullOrEmpty(recordType)) q = q.Where(a => a.RecordType == recordType);
            if (!string.IsNullOrEmpty(recordId)) q = q.Where(a => a.RecordId == recordId);
            return await q.OrderByDescending(a => a.OccurredAt).Take(Math.Clamp(take ?? 100, 1, 500)).ToListAsync(ct);
        }).RequirePermission(CorePermissions.AuditRead).WithTags("Security");
    }

    // ---- Parties and items -----------------------------------------------------
    private static void MapMasterData(RouteGroupBuilder api)
    {
        var p = api.MapGroup("/parties").WithTags("Master data");
        p.MapGet("", (string? search, PartyRoles? role, int? page, int? pageSize, ISender s, CancellationToken ct) => s.Send(new ListParties(search, role, page ?? 1, pageSize ?? 25), ct))
            .RequirePermission(CorePermissions.PartiesRead);
        p.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetParty(id), ct)).RequirePermission(CorePermissions.PartiesRead);
        p.MapPost("", async (PartyInput i, ISender s, CancellationToken ct) => { var r = await s.Send(new CreateParty(i), ct); return Results.Created($"/api/v1/parties/{r.Id}", r); })
            .RequirePermission(CorePermissions.PartiesWrite);
        p.MapPut("/{id:guid}", (Guid id, PartyInput i, ISender s, CancellationToken ct) => s.Send(new UpdateParty(id, i), ct)).RequirePermission(CorePermissions.PartiesWrite);

        var items = api.MapGroup("/items").WithTags("Master data");
        items.MapGet("", async (AppDbContext db, ICustomFieldService cf, CancellationToken ct) =>
        {
            var defs = await cf.GetDefinitionsAsync("item", ct);
            return (await db.Items.AsNoTracking().OrderBy(i => i.Sku).ToListAsync(ct))
                .Select(i => new { i.Id, i.Sku, i.Name, type = i.Type.ToString(), i.Uom, i.SalesPrice, i.PurchasePrice, i.TaxCodeId, i.IsActive, customFields = CustomFieldValidator.Project(i.CustomFields, defs) });
        }).RequirePermission(CorePermissions.ItemsRead);
        items.MapPost("", async (ItemInput i, AppDbContext db, ICustomFieldService cf, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(i.Sku) || string.IsNullOrWhiteSpace(i.Name)) throw new FieldValidationException("sku", "SKU and name are required.");
            if (await db.Items.AnyAsync(x => x.Sku == i.Sku, ct)) throw new ConflictException("An item with that SKU exists.");
            var item = new Item { Sku = i.Sku.Trim(), Name = i.Name.Trim(), Type = i.Type, Uom = i.Uom, SalesPrice = i.SalesPrice, PurchasePrice = i.PurchasePrice, TaxCodeId = i.TaxCodeId, IsActive = i.IsActive };
            await cf.ApplyAsync("item", item, i.CustomFields, true, ct);
            db.Items.Add(item);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/items/{item.Id}", new { item.Id, item.Sku, item.Name, customFields = await cf.ReadAsync("item", item, ct) });
        }).RequirePermission(CorePermissions.ItemsWrite);
    }

    // ---- Finance reference data ---------------------------------------------------
    private static void MapFinance(RouteGroupBuilder api)
    {
        var f = api.MapGroup("/finance").WithTags("Finance");
        f.MapGet("/accounts", async (AppDbContext db, CancellationToken ct) =>
            await db.Accounts.AsNoTracking().OrderBy(a => a.Code)
                .Select(a => new { a.Id, a.Code, a.Name, type = a.Type.ToString(), a.ReportingGroup, a.SystemRole, a.IsBank, a.IsSystem, a.IsActive }).ToListAsync(ct))
            .RequirePermission(CorePermissions.AccountsRead);
        f.MapGet("/tax-codes", async (AppDbContext db, CancellationToken ct) =>
            await db.TaxCodes.AsNoTracking().OrderBy(t => t.Code).Select(t => new { t.Id, t.Code, t.Name, kind = t.Kind.ToString(), t.Rate, t.IsDefault, t.IsRecoverable }).ToListAsync(ct))
            .RequirePermission(CorePermissions.TaxRead);
        f.MapGet("/currencies", async (AppDbContext db, CancellationToken ct) =>
            await db.Currencies.AsNoTracking().OrderBy(c => c.Code).Select(c => new { c.Code, c.Name, c.Symbol, c.Decimals }).ToListAsync(ct))
            .RequirePermission(CorePermissions.CurrenciesRead);
    }

    // ---- Custom fields ---------------------------------------------------------------
    private static void MapCustomFields(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/custom-fields").WithTags("Customization");
        g.MapGet("/entity-types", (ICustomizableEntityRegistry r, IModuleAccess access) =>
            r.All.Select(d => new { d.Key, d.Label, module = d.ModuleKey })).RequirePermission(CorePermissions.CustomFieldsRead);
        g.MapGet("", (string entityType, bool? includeInactive, ISender s, CancellationToken ct) => s.Send(new ListCustomFieldDefinitions(entityType, includeInactive ?? false), ct))
            .RequirePermission(CorePermissions.CustomFieldsRead);
        // Form metadata: active definitions for rendering, readable by anyone who can read the entity type.
        g.MapGet("/form/{entityType}", (string entityType, ISender s, ICustomizableEntityRegistry r, ICurrentUser user, CancellationToken ct) =>
        {
            var def = r.Find(entityType) ?? throw new NotFoundException();
            if (def.Permission is { } perm && !user.HasPermission(perm)) throw new ForbiddenException();
            return s.Send(new GetFormFields(entityType), ct);
        }).RequireAuthorization();
        g.MapPost("", async (CustomFieldDefinitionInput i, ISender s, CancellationToken ct) => { var r = await s.Send(new CreateCustomFieldDefinition(i), ct); return Results.Created($"/api/v1/custom-fields/{r.Id}", r); })
            .RequirePermission(CorePermissions.CustomFieldsManage);
        g.MapPut("/{id:guid}", (Guid id, CustomFieldDefinitionInput i, ISender s, CancellationToken ct) => s.Send(new UpdateCustomFieldDefinition(id, i), ct))
            .RequirePermission(CorePermissions.CustomFieldsManage);
        g.MapDelete("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new DeactivateCustomFieldDefinition(id), ct))
            .RequirePermission(CorePermissions.CustomFieldsManage);
        g.MapGet("/values/{entityType}/{id:guid}", (string entityType, Guid id, ISender s, CancellationToken ct) => s.Send(new GetCustomFieldValues(entityType, id), ct))
            .RequireAuthorization();
        g.MapPut("/values/{entityType}/{id:guid}", (string entityType, Guid id, JsonObject values, ISender s, CancellationToken ct) => s.Send(new SetCustomFieldValues(entityType, id, values), ct))
            .RequireAuthorization();
    }

    // ---- Approvals, notifications, attachments ---------------------------------------
    private static void MapWorkflow(RouteGroupBuilder api)
    {
        var a = api.MapGroup("/approvals").WithTags("Workflow");
        a.MapGet("", async (string? scope, AppDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var q = db.ApprovalRequests.AsNoTracking();
            q = scope == "mine" ? q.Where(r => r.RequestedBy == user.UserId)
                : user.HasPermission(CorePermissions.ApprovalsDecide) ? q.Where(r => r.Status == Domain.Platform.ApprovalStatus.Pending && r.RequestedBy != user.UserId)
                : q.Where(r => r.RequestedBy == user.UserId);
            return await q.OrderByDescending(r => r.RequestedAt).Take(200).ToListAsync(ct);
        }).RequireAuthorization();
        a.MapPost("", async (ApprovalInput i, IApprovalService s, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(i.Subject)) throw new FieldValidationException("subject", "Subject is required.");
            var r = await s.SubmitAsync("general", null, i.Subject, i.Amount, i.Justification, i.LegalEntityId, ct);
            return Results.Created($"/api/v1/approvals/{r.Id}", r);
        }).RequireAuthorization();
        a.MapPost("/{id:guid}/approve", (Guid id, DecisionInput? d, IApprovalService s, CancellationToken ct) => s.DecideAsync(id, true, d?.Comment, ct)).RequirePermission(CorePermissions.ApprovalsDecide);
        a.MapPost("/{id:guid}/reject", (Guid id, DecisionInput? d, IApprovalService s, CancellationToken ct) => s.DecideAsync(id, false, d?.Comment, ct)).RequirePermission(CorePermissions.ApprovalsDecide);
        a.MapPost("/{id:guid}/withdraw", (Guid id, IApprovalService s, CancellationToken ct) => s.WithdrawAsync(id, ct)).RequireAuthorization();

        var n = api.MapGroup("/notifications").WithTags("Workflow");
        n.MapGet("", async (AppDbContext db, ICurrentUser user, CancellationToken ct) => new
        {
            unread = await db.Notifications.CountAsync(x => x.UserId == user.UserId && x.ReadAt == null, ct),
            items = await db.Notifications.AsNoTracking().Where(x => x.UserId == user.UserId).OrderByDescending(x => x.CreatedAt).Take(50)
                .Select(x => new { x.Id, x.Title, x.Body, x.Link, x.Severity, x.CreatedAt, x.ReadAt }).ToListAsync(ct),
        }).RequireAuthorization();
        n.MapPost("/{id:guid}/read", async (Guid id, AppDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var x = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.UserId, ct) ?? throw new NotFoundException();
            x.ReadAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        var f = api.MapGroup("/attachments").WithTags("Workflow");
        f.MapGet("", async (string ownerType, Guid ownerId, AppDbContext db, CancellationToken ct) =>
            await db.Attachments.AsNoTracking().Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId)
                .Select(x => new { x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAt }).ToListAsync(ct))
            .RequirePermission(CorePermissions.AttachmentsRead);
        f.MapPost("", async (HttpRequest req, AppDbContext db, IFileStore store, ISettingsService settings, CancellationToken ct) =>
        {
            var form = await req.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault() ?? throw new FieldValidationException("file", "A file is required.");
            if (!Guid.TryParse(form["ownerId"], out var ownerId) || string.IsNullOrWhiteSpace(form["ownerType"])) throw new FieldValidationException("ownerId", "Owner type and id are required.");
            var maxMb = await settings.GetAsync<int?>("core.maxAttachmentMb", ct: ct) ?? 10;
            if (file.Length > maxMb * 1024L * 1024L) throw new FieldValidationException("file", $"Files can be at most {maxMb} MB.");
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) throw new FieldValidationException("file", "This file type is not allowed.");
            await using var stream = file.OpenReadStream();
            var key = await store.SaveAsync(stream, file.FileName, ct);
            var att = new Domain.Platform.Attachment { OwnerType = form["ownerType"]!, OwnerId = ownerId, FileName = Path.GetFileName(file.FileName), ContentType = file.ContentType, SizeBytes = file.Length, StorageKey = key };
            db.Attachments.Add(att);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/attachments/{att.Id}", new { att.Id, att.FileName, att.SizeBytes });
        }).RequirePermission(CorePermissions.AttachmentsWrite).DisableAntiforgery();
        f.MapGet("/{id:guid}/download", async (Guid id, AppDbContext db, IFileStore store, CancellationToken ct) =>
        {
            var att = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
            return Results.File(await store.OpenAsync(att.StorageKey, ct), att.ContentType, att.FileName);
        }).RequirePermission(CorePermissions.AttachmentsRead);
        f.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var att = await db.Attachments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
            db.Attachments.Remove(att); // soft delete; the file is kept for audit
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequirePermission(CorePermissions.AttachmentsWrite);
    }

    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".txt", ".csv", ".xlsx", ".xls", ".docx", ".doc", ".zip"];

    private static void MapLe(LegalEntityInput i, LegalEntity e)
    {
        if (string.IsNullOrWhiteSpace(i.Name)) throw new FieldValidationException("name", "Name is required.");
        if ((i.CountryCode ?? "").Length != 2) throw new FieldValidationException("countryCode", "Use a 2-letter ISO country code.");
        if ((i.BaseCurrency ?? "").Length != 3) throw new FieldValidationException("baseCurrency", "Use a 3-letter ISO currency code.");
        e.Name = i.Name.Trim(); e.LegalName = i.LegalName; e.CountryCode = i.CountryCode!.ToUpperInvariant(); e.BaseCurrency = i.BaseCurrency!.ToUpperInvariant();
        e.TaxRegistrationNo = i.TaxRegistrationNo; e.IsTaxRegistered = i.IsTaxRegistered; e.RegistrationNo = i.RegistrationNo; e.Address = i.Address;
        e.Email = i.Email; e.Phone = i.Phone; e.IsActive = i.IsActive;
    }

    private static object LeDto(LegalEntity e, JsonObject cf) => new
    {
        e.Id, e.Code, e.Name, e.LegalName, e.CountryCode, e.BaseCurrency, e.TaxRegistrationNo, e.IsTaxRegistered, e.RegistrationNo,
        e.Address, e.Email, e.Phone, e.BooksLockedTo, e.IsActive, customFields = cf,
    };
}

/// <summary>Core navigation (industry-neutral). Modules add their groups through IModule.Menu.</summary>
public static class CoreMenu
{
    public static IReadOnlyList<MenuItemDefinition> Items { get; } =
    [
        new("core.overview", "Overview", "/", "dashboard", null, 10, "workspace", "Workspace", 10),
        new("core.approvals", "Approvals", "/approvals", "check-square", null, 20, "workspace", "Workspace", 10),
        new("core.parties", "Parties", "/parties", "team", CorePermissions.PartiesRead, 10, "masterdata", "Master data", 20),
        new("core.items", "Items", "/catalog/items", "shopping", CorePermissions.ItemsRead, 20, "masterdata", "Master data", 20),
        new("finance.accounts", "Chart of accounts", "/finance/accounts", "account-book", CorePermissions.AccountsRead, 10, "finance", "Finance", 50),
        new("finance.taxcodes", "Tax codes", "/finance/tax-codes", "percentage", CorePermissions.TaxRead, 20, "finance", "Finance", 50),
        new("core.legalentities", "Legal entities", "/admin/legal-entities", "apartment", CorePermissions.EntitiesRead, 10, "admin", "Administration", 900),
        new("core.businessunits", "Business units", "/admin/business-units", "cluster", CorePermissions.BusinessUnitsRead, 20, "admin", "Administration", 900),
        new("core.users", "Users", "/admin/users", "user", CorePermissions.UsersRead, 30, "admin", "Administration", 900),
        new("core.roles", "Roles", "/admin/roles", "safety", CorePermissions.RolesRead, 40, "admin", "Administration", 900),
        new("core.customfields", "Custom fields", "/admin/custom-fields", "form", CorePermissions.CustomFieldsRead, 50, "admin", "Administration", 900),
        new("core.modules", "Modules", "/admin/modules", "appstore-add", CorePermissions.SettingsRead, 60, "admin", "Administration", 900),
        new("core.settings", "Settings", "/admin/settings", "setting", CorePermissions.SettingsRead, 70, "admin", "Administration", 900),
        new("core.audit", "Audit log", "/admin/audit", "audit", CorePermissions.AuditRead, 80, "admin", "Administration", 900),
    ];
}
