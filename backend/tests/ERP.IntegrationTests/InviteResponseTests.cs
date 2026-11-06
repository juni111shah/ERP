using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Security;

namespace ERP.IntegrationTests;

/// <summary>Accounts are global, so an invite must not read back the name another workspace knows them by.</summary>
public class InviteResponseTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<string> RoleAsync(HttpClient c)
        => (string)(await HrKit.Ok(await c.PostAsJsonAsync("/api/v1/roles", new RoleInput(HrKit.Unique("Invite"), null, [CorePermissions.PartiesRead])), HttpStatusCode.Created))["id"]!;

    [Fact]
    public async Task Inviting_an_existing_account_does_not_reveal_its_full_name()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var email = HrKit.Unique("known") + "@demo.local";
        await HrKit.Ok(await first.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Private Realname", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { await RoleAsync(first) } }), HttpStatusCode.Created);

        var (second, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var invited = await second.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Typed By Inviter", roleIds = new[] { await RoleAsync(second) } });
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var text = await invited.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Private Realname", text);
        Assert.Equal("Typed By Inviter", (string)ApiFactory.Unwrap(JsonNode.Parse(text)!)["fullName"]!);
    }
}
