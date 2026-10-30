using System.Net.Http.Json;

namespace ERP.IntegrationTests;

/// <summary>The member list, the single-user summary after a change, and history names (TASK-122).</summary>
public class UserDirectoryTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Members_list_carries_each_users_own_roles_and_a_role_change_returns_just_that_user()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var roles = (await ApiFactory.Json(await admin.GetAsync("/api/v1/roles"))).AsArray();
        var viewer = roles.First(r => (string?)r!["name"] == "Viewer")!;
        var approver = roles.First(r => (string?)r!["name"] == "Approver")!;
        var email = $"dir{Guid.NewGuid():N}"[..14] + "@demo.local";
        var created = await ApiFactory.Json(await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Zed Directory", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { (string)viewer["id"]! } }));
        var id = (string)created["id"]!;

        var list = (await ApiFactory.Json(await admin.GetAsync("/api/v1/users"))).AsArray();
        var mine = list.Single(u => (string)u!["id"]! == id)!;
        Assert.Equal(new[] { "Viewer" }, mine["roles"]!.AsArray().Select(r => (string)r!).ToArray());
        var names = list.Select(u => (string)u!["fullName"]!).ToList();
        Assert.Equal(names.OrderBy(n => n).ToList(), names); // ordered by name

        var changed = await ApiFactory.Json(await admin.PutAsJsonAsync($"/api/v1/users/{id}/roles", new { roleIds = new[] { (string)approver["id"]! } }));
        Assert.Equal(id, (string)changed["id"]!);
        Assert.Equal(new[] { "Approver" }, changed["roles"]!.AsArray().Select(r => (string)r!).ToArray());
    }
}
