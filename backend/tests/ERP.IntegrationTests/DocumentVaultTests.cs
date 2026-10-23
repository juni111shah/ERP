using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Tracked documents: category and expiry on attachments, the vault list and expiry reminders.</summary>
public class DocumentVaultTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static MultipartFormDataContent Form(Guid ownerId, string name, string category, string? expires)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("party"), "ownerType" }, { new StringContent(ownerId.ToString()), "ownerId" },
            { new StringContent(category), "category" }, { new StringContent("test document"), "description" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes("content")), "file", name },
        };
        if (expires is not null) form.Add(new StringContent(expires), "expiresOn");
        return form;
    }

    [Fact]
    public async Task Documents_keep_category_and_expiry_appear_in_the_vault_and_trigger_reminders()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var owner = Guid.NewGuid();
        var soon = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd");
        var name = "licence-" + Guid.NewGuid().ToString("N")[..6] + ".pdf";
        var up = await c.PostAsync("/api/v1/attachments", Form(owner, name, "Trade licence", soon));
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        var created = ApiFactory.Unwrap(JsonNode.Parse(await up.Content.ReadAsStringAsync())!);
        Assert.Equal("Trade licence", (string)created["category"]!);
        Assert.Equal(soon, (string)created["expiresOn"]!);
        await c.PostAsync("/api/v1/attachments", Form(owner, "no-expiry.pdf", "Contract", null));

        var vault = ApiFactory.Unwrap(JsonNode.Parse(await (await c.GetAsync($"/api/v1/attachments/vault?search={Uri.EscapeDataString(name)}")).Content.ReadAsStringAsync())!).AsArray();
        var row = Assert.Single(vault)!;
        Assert.Equal(soon, (string)row["expiresOn"]!);

        var sent = ApiFactory.Unwrap(JsonNode.Parse(await (await c.PostAsync("/api/v1/messages/send-reminders", null)).Content.ReadAsStringAsync())!);
        Assert.True((int)sent["sent"]! >= 1);
        var messages = ApiFactory.Unwrap(JsonNode.Parse(await (await c.GetAsync("/api/v1/messages?pageSize=100")).Content.ReadAsStringAsync())!).AsArray();
        Assert.Contains(messages, m => ((string)m!["subject"]!).Contains(name));

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/api/v1/attachments", Form(owner, "x.pdf", new string('a', 60), null))).StatusCode);
    }
}
