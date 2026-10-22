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
        var ownerId = Guid.NewGuid();
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
