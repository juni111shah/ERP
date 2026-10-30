using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace ERP.IntegrationTests;

/// <summary>
/// Every write/read endpoint goes Endpoint -> MediatR -> Repository -> DB. These tests drive the refactored endpoints end to end
/// (validation, conflicts, tenancy, envelope) so the layering never costs behaviour.
/// </summary>
public class CqrsEndpointTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> AdminAsync() => (await f.SignInAsync(ApiFactory.AdminEmail)).Client;

    [Fact]
    public async Task Items_are_created_validated_and_unique_by_sku()
    {
        var admin = await AdminAsync();
        var sku = "SKU-" + Guid.NewGuid().ToString("N")[..8];
        var created = await admin.PostAsJsonAsync("/api/v1/items", new { sku, name = "Steel bracket", type = "Product", uom = "ea", salesPrice = 12.5m });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await ApiFactory.Json(created);
        Assert.Equal(sku, (string?)item["sku"]);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/items", new { sku, name = "Dup", type = "Product", uom = "ea" })).StatusCode);

        var bad = await admin.PostAsJsonAsync("/api/v1/items", new { sku = "", name = "", type = "Product", uom = "ea" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await ApiFactory.Envelope(bad))["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("sku"), "validation errors are keyed by the field name, not by the command wrapper");

        var list = (await ApiFactory.Json(await admin.GetAsync($"/api/v1/items?search={sku}"))).AsArray();
        Assert.Contains(list, i => (string?)i!["sku"] == sku);
    }

    [Fact]
    public async Task Legal_entities_and_business_units_follow_the_command_pipeline()
    {
        var admin = await AdminAsync();
        var created = await admin.PostAsJsonAsync("/api/v1/legal-entities", new
        {
            name = "Branch Co", countryCode = "pk", baseCurrency = "pkr", isTaxRegistered = false,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var entity = await ApiFactory.Json(created);
        Assert.Equal("PK", (string?)entity["countryCode"]); // normalised by the handler
        var id = (string)entity["id"]!;

        var updated = await admin.PutAsJsonAsync($"/api/v1/legal-entities/{id}", new { name = "Branch Co Ltd", countryCode = "PK", baseCurrency = "PKR", isTaxRegistered = true, taxRegistrationNo = "123" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Branch Co Ltd", (string?)(await ApiFactory.Json(updated))["name"]);

        var invalid = await admin.PostAsJsonAsync("/api/v1/legal-entities", new { name = "", countryCode = "PAK", baseCurrency = "PK", isTaxRegistered = false });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var errors = (await ApiFactory.Envelope(invalid))["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("name") && errors.ContainsKey("countryCode") && errors.ContainsKey("baseCurrency"));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/v1/legal-entities/{Guid.NewGuid()}", new { name = "X", countryCode = "PK", baseCurrency = "PKR", isTaxRegistered = false })).StatusCode);

        var unit = await admin.PostAsJsonAsync("/api/v1/business-units", new { legalEntityId = id, code = "BR-01", name = "Main branch" });
        Assert.Equal(HttpStatusCode.Created, unit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/business-units", new { legalEntityId = Guid.NewGuid(), code = "BR-02", name = "Ghost" })).StatusCode);
        var units = (await ApiFactory.Json(await admin.GetAsync("/api/v1/business-units"))).AsArray();
        Assert.Contains(units, u => (string?)u!["code"] == "BR-01");
    }

    [Fact]
    public async Task Settings_are_listed_and_updated_with_per_key_permissions()
    {
        var admin = await AdminAsync();
        var list = (await ApiFactory.Json(await admin.GetAsync("/api/v1/settings"))).AsArray();
        Assert.Contains(list, s => (string?)s!["key"] == "core.maxAttachmentMb");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, int> { ["core.maxAttachmentMb"] = 5 })).StatusCode);
        var after = (await ApiFactory.Json(await admin.GetAsync("/api/v1/settings"))).AsArray();
        Assert.Equal(5, (int)after.First(s => (string?)s!["key"] == "core.maxAttachmentMb")!["value"]!);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, int> { ["core.doesNotExist"] = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, int> { ["realestate.notEnabled"] = 1 })).StatusCode); // module not enabled in this workspace
    }

    [Fact]
    public async Task Attachments_are_uploaded_listed_downloaded_and_deleted()
    {
        var admin = await AdminAsync();
        var partyRes = await admin.PostAsJsonAsync("/api/v1/parties", new { name = "Attachment Test Party", kind = "Company", roles = "Customer" });
        var ownerId = Guid.Parse((string)(await ApiFactory.Json(partyRes))["id"]!);
        MultipartFormDataContent Form(string name, string content, string ownerType = "party")
        {
            var form = new MultipartFormDataContent { { new StringContent(ownerType), "ownerType" }, { new StringContent(ownerId.ToString()), "ownerId" } };
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)) { Headers = { ContentType = new("text/plain") } }, "file", name);
            return form;
        }

        var up = await admin.PostAsync("/api/v1/attachments", Form("notes.txt", "hello"));
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        var id = (string)(await ApiFactory.Json(up))["id"]!;

        var list = (await ApiFactory.Json(await admin.GetAsync($"/api/v1/attachments?ownerType=party&ownerId={ownerId}"))).AsArray();
        Assert.Single(list);
        Assert.Equal("hello", await admin.GetStringAsync($"/api/v1/attachments/{id}/download"));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/attachments", Form("virus.exe", "MZ"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/attachments", new MultipartFormDataContent())).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/attachments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/attachments/{id}/download")).StatusCode);
    }

    [Fact]
    public async Task Notifications_can_be_marked_read_only_by_their_owner()
    {
        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/v1/notifications/{Guid.NewGuid()}/read", null)).StatusCode);
        var list = await ApiFactory.Json(await admin.GetAsync("/api/v1/notifications"));
        Assert.NotNull(list["unread"]);
        Assert.NotNull(list["items"]);
    }

    [Fact]
    public async Task Permissions_modules_and_entity_types_are_exposed_through_queries()
    {
        var admin = await AdminAsync();
        var perms = (await ApiFactory.Json(await admin.GetAsync("/api/v1/permissions"))).AsArray();
        Assert.Contains(perms, p => (string?)p!["key"] == "core.customfields.manage");

        var types = (await ApiFactory.Json(await admin.GetAsync("/api/v1/custom-fields/entity-types"))).AsArray();
        Assert.Contains(types, t => (string?)t!["key"] == "party");

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/v1/modules/nope/enable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/custom-fields/form/not-an-entity")).StatusCode);
    }

    [Fact]
    public async Task Password_can_be_changed_through_the_command_and_the_new_one_signs_in()
    {
        var email = $"changer{Guid.NewGuid():N}@demo.local".Substring(0, 24) + "@demo.local";
        var admin = await AdminAsync();
        var roles = (await ApiFactory.Json(await admin.GetAsync("/api/v1/roles"))).AsArray();
        var viewer = (string)roles.First(r => (string?)r!["name"] == "Viewer")!["id"]!;
        var invite = await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Changer", temporaryPassword = "Temp#Passw0rd1", roleIds = new[] { viewer } });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);

        var (user, _) = await f.SignInAsync(email, "Temp#Passw0rd1");
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "wrong", newPassword = "Brand#New2026x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Temp#Passw0rd1", newPassword = "Brand#New2026x" })).StatusCode);
        await f.SignInAsync(email, "Brand#New2026x");
    }

    [Fact]
    public async Task Login_without_credentials_is_a_field_validation_error()
    {
        var res = await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await ApiFactory.Envelope(res))["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("email") && errors.ContainsKey("password"));
    }
}

public class OnboardingDataTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Countries_and_currencies_are_public_and_mark_installed_packs()
    {
        var countries = (await ApiFactory.Json(await f.Client().GetAsync("/api/v1/platform/countries"))).AsArray();
        Assert.Contains(countries, c => (string?)c!["code"] == "AE" && (bool)c["hasPack"]! && (string?)c["defaultCurrency"] == "AED");
        Assert.Contains(countries, c => (string?)c!["code"] == "PK" && (bool)c["hasPack"]!);
        Assert.Contains(countries, c => (string?)c!["code"] == "FR" && !(bool)c["hasPack"]!);
        Assert.Equal(countries.Count, countries.Select(c => (string?)c!["code"]).Distinct().Count());
        var currencies = (await ApiFactory.Json(await f.Client().GetAsync("/api/v1/platform/currencies"))).AsArray();
        Assert.Contains(currencies, c => (string?)c!["code"] == "AED");
        Assert.Contains(currencies, c => (string?)c!["code"] == "EUR");
    }

    [Fact]
    public async Task Each_pack_country_gets_its_own_tax_codes_and_fields_and_other_countries_get_the_neutral_default()
    {
        foreach (var (code, currency, tax, field) in new[] { ("SA", "SAR", "VAT15", "cf_sa_vat_no"), ("IN", "INR", "GST18", "cf_in_gstin"), ("GB", "GBP", "VAT20", "cf_gb_vat_no"), ("US", "USD", "ST", "cf_us_ein"), ("FR", "EUR", "STD", "") })
        {
            var key = $"cp-{code.ToLower()}-{Guid.NewGuid():N}"[..14];
            var created = await f.Client().PostAsJsonAsync("/api/v1/platform/onboarding", new
            {
                workspaceName = "Pack " + code, signInKey = key, industry = "generic", countryCode = code, baseCurrency = currency, timeZone = "UTC",
                adminEmail = $"admin+{key}@example.test", adminName = "Admin", adminPassword = "Qa#Passw0rd1",
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var (c, _) = await f.SignInAsync($"admin+{key}@example.test", "Qa#Passw0rd1");
            var taxes = (await ApiFactory.Json(await c.GetAsync("/api/v1/finance/tax-codes"))).AsArray();
            Assert.Contains(taxes, t => (string?)t!["code"] == tax);
            var form = (await ApiFactory.Json(await c.GetAsync("/api/v1/custom-fields/form/party"))).AsArray();
            if (field.Length > 0) Assert.Contains(form, d => (string?)d!["key"] == field);
            else Assert.DoesNotContain(form, d => System.Text.RegularExpressions.Regex.IsMatch((string)d!["key"]!, "^cf_[a-z]{2}_")); // no country field without a pack
        }
    }
}

/// <summary>Updates for the admin and master-data screens: every one goes through a command with its own rules.</summary>
public class AdminCommandTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> AdminAsync() => (await f.SignInAsync(ApiFactory.AdminEmail)).Client;

    [Fact]
    public async Task Items_can_be_read_and_updated_with_a_unique_sku()
    {
        var admin = await AdminAsync();
        string Sku() => "UP-" + Guid.NewGuid().ToString("N")[..8];
        var (a, b) = (Sku(), Sku());
        var first = await ApiFactory.Json(await admin.PostAsJsonAsync("/api/v1/items", new { sku = a, name = "A", type = "Product", uom = "ea" }));
        await admin.PostAsJsonAsync("/api/v1/items", new { sku = b, name = "B", type = "Product", uom = "ea" });
        var id = (string)first["id"]!;

        var updated = await admin.PutAsJsonAsync($"/api/v1/items/{id}", new { sku = a, name = "A renamed", type = "Service", uom = "hr", salesPrice = 99.5m, isActive = false });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var item = await ApiFactory.Json(await admin.GetAsync($"/api/v1/items/{id}"));
        Assert.Equal(("A renamed", "service", false), ((string?)item["name"], (string?)item["type"], (bool)item["isActive"]!));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/items/{id}", new { sku = b, name = "A", type = "Product", uom = "ea" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/v1/items/{Guid.NewGuid()}", new { sku = Sku(), name = "X", type = "Product", uom = "ea" })).StatusCode);
    }

    [Fact]
    public async Task Tax_codes_are_created_edited_and_keep_a_single_default()
    {
        var admin = await AdminAsync();
        var code = "T" + Guid.NewGuid().ToString("N")[..6];
        var created = await admin.PostAsJsonAsync("/api/v1/finance/tax-codes", new { code, name = "Custom 7%", kind = "vat", rate = 7, isDefault = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (string)(await ApiFactory.Json(created))["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/finance/tax-codes", new { code, name = "Dup", kind = "vat", rate = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/finance/tax-codes", new { code = "X1", name = "Bad", kind = "vat", rate = 150 })).StatusCode);

        var taxes = (await ApiFactory.Json(await admin.GetAsync("/api/v1/finance/tax-codes?pageSize=100"))).AsArray();
        Assert.Single(taxes.Where(t => (bool)t!["isDefault"]!)); // making this one the default cleared the previous default
        Assert.Equal(id, (string?)taxes.Single(t => (bool)t!["isDefault"]!)!["id"]);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/finance/tax-codes/{id}", new { code, name = "Custom 8%", kind = "vat", rate = 8, isDefault = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/finance/tax-codes/{id}", new { code, name = "Inactive default", kind = "vat", rate = 8, isDefault = true, isActive = false })).StatusCode);
    }

    [Fact]
    public async Task Accounts_are_managed_but_system_accounts_keep_their_identity()
    {
        var admin = await AdminAsync();
        var code = "9" + Guid.NewGuid().ToString("N")[..5];
        var created = await admin.PostAsJsonAsync("/api/v1/finance/accounts", new { code, name = "Marketing", type = "expense", reportingGroup = "Operating expenses" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (string)(await ApiFactory.Json(created))["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/finance/accounts", new { code, name = "Dup", type = "expense" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/finance/accounts", new { code = "9" + Guid.NewGuid().ToString("N")[..5], name = "Orphan", type = "expense", parentId = Guid.NewGuid() })).StatusCode);

        var child = await ApiFactory.Json(await admin.PostAsJsonAsync("/api/v1/finance/accounts", new { code = code + "1", name = "Ads", type = "expense", parentId = id }));
        // A parent can never become a descendant of its own child (no loops).
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/finance/accounts/{id}", new { code, name = "Marketing", type = "expense", parentId = (string)child["id"]! })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/finance/accounts/{id}", new { code, name = "Marketing", type = "expense", parentId = id })).StatusCode);

        var system = (await ApiFactory.Json(await admin.GetAsync("/api/v1/finance/accounts?pageSize=200"))).AsArray();
        var ar = system.First(a => (string?)a!["systemRole"] == "ar.control")!;
        var arId = (string)ar["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/finance/accounts/{arId}", new { code = "1199", name = "AR", type = "asset" })).StatusCode); // code is fixed
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/finance/accounts/{arId}", new { code = (string)ar["code"]!, name = "AR", type = "asset", isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/finance/accounts/{arId}", new { code = (string)ar["code"]!, name = "Trade receivables", type = "asset" })).StatusCode); // renaming is fine
    }

    [Fact]
    public async Task Business_units_can_be_edited_within_their_legal_entity()
    {
        var admin = await AdminAsync();
        var me = await ApiFactory.Json(await admin.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;
        var code = "BU" + Guid.NewGuid().ToString("N")[..5];
        var other = "BU" + Guid.NewGuid().ToString("N")[..5];
        var created = await ApiFactory.Json(await admin.PostAsJsonAsync("/api/v1/business-units", new { legalEntityId = entity, code, name = "Branch A" }));
        await admin.PostAsJsonAsync("/api/v1/business-units", new { legalEntityId = entity, code = other, name = "Branch B" });
        var id = (string)created["id"]!;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/business-units/{id}", new { legalEntityId = entity, code, name = "Branch A (Dubai)", isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/business-units/{id}", new { legalEntityId = entity, code = other, name = "Clash" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/business-units/{id}", new { legalEntityId = Guid.NewGuid(), code, name = "Moved" })).StatusCode);
    }

    [Fact]
    public async Task Custom_roles_are_editable_built_in_roles_are_not()
    {
        var admin = await AdminAsync();
        var roles = (await ApiFactory.Json(await admin.GetAsync("/api/v1/roles?pageSize=100"))).AsArray();
        var builtIn = (string)roles.First(r => (bool)r!["isBuiltIn"]!)!["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/roles/{builtIn}", new { name = "Hacked", permissions = new[] { "core.parties.read" } })).StatusCode);

        var name = "Custom " + Guid.NewGuid().ToString("N")[..6];
        var created = await ApiFactory.Json(await admin.PostAsJsonAsync("/api/v1/roles", new { name, permissions = new[] { "core.parties.read" } }));
        var id = (string)created["id"]!;
        var edit = await admin.PutAsJsonAsync($"/api/v1/roles/{id}", new { name = name + " v2", description = "Edited", permissions = new[] { "core.parties.read", "core.items.read" } });
        Assert.True(edit.StatusCode == HttpStatusCode.OK, await edit.Content.ReadAsStringAsync());
        var role = (await ApiFactory.Json(await admin.GetAsync("/api/v1/roles?pageSize=100"))).AsArray().First(r => (string?)r!["id"] == id)!;
        Assert.Equal(2, role["permissions"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/roles/{id}", new { name, permissions = new[] { "nope.nope.nope" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/roles/{id}", new { name = (string)roles.First()!["name"]!, permissions = new[] { "core.parties.read" } })).StatusCode);
    }

    [Fact]
    public async Task Users_roles_and_active_state_change_but_the_last_administrator_is_protected()
    {
        var admin = await AdminAsync();
        var roles = (await ApiFactory.Json(await admin.GetAsync("/api/v1/roles?pageSize=100"))).AsArray();
        string RoleId(string name) => (string)roles.First(r => (string?)r!["name"] == name)!["id"]!;
        var email = $"mgr{Guid.NewGuid():N}"[..12] + "@demo.local";
        var invited = await ApiFactory.Json(await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Manager One", temporaryPassword = "Temp#Passw0rd1", roleIds = new[] { RoleId("Viewer") } }));
        var userId = (string)invited["id"]!;

        var changed = await ApiFactory.Json(await admin.PutAsJsonAsync($"/api/v1/users/{userId}/roles", new { roleIds = new[] { RoleId("Accountant"), RoleId("Approver") } }));
        Assert.Equal(["Accountant", "Approver"], changed["roles"]!.AsArray().Select(r => (string)r!).OrderBy(x => x));
        var inactive = await ApiFactory.Json(await admin.PostAsJsonAsync($"/api/v1/users/{userId}/active", new { isActive = false }));
        Assert.False((bool)inactive["isActive"]!);
        Assert.True((bool)(await ApiFactory.Json(await admin.PostAsJsonAsync($"/api/v1/users/{userId}/active", new { isActive = true })))["isActive"]!);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/active", new { isActive = false })).StatusCode);

        // The only administrator cannot deactivate themselves or lose the admin role.
        var me = await ApiFactory.Json(await admin.GetAsync("/api/v1/me"));
        var myId = (string)me["user"]!["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/users/{myId}/active", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/users/{myId}/roles", new { roleIds = new[] { RoleId("Viewer") } })).StatusCode);
    }
}
