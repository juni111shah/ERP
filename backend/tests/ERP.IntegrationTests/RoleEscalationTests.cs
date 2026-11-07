using System.Net;
using System.Net.Http.Json;
using ERP.Application.Security;

namespace ERP.IntegrationTests;

/// <summary>A delegated admin (users or roles write, no wildcard) can hand out no more than they hold, and cannot promote themselves.</summary>
public class RoleEscalationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Pw = "Temp#Passw0rd";

    private static readonly string[] Delegated = [CorePermissions.UsersRead, CorePermissions.UsersWrite, CorePermissions.RolesRead, CorePermissions.RolesWrite, CorePermissions.PartiesRead];

    private sealed record Setup(HttpClient Admin, HttpClient Helpdesk, string HelpdeskId, string HelpdeskRole, string AdminRole, string SmallRole, string BigRole, string MemberId);

    private static async Task<string> RoleAsync(HttpClient admin, string label, params string[] permissions)
        => (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/roles", new RoleInput(HrKit.Unique(label), null, permissions)), HttpStatusCode.Created))["id"]!;

    private static async Task<string> InviteAsync(HttpClient by, string role)
        => (string)(await HrKit.Ok(await by.PostAsJsonAsync("/api/v1/users", new { email = HrKit.Unique("member") + "@demo.local", fullName = "A Member", temporaryPassword = Pw, roleIds = new[] { role } }), HttpStatusCode.Created))["id"]!;

    private async Task<Setup> StartAsync()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var roles = (await HrKit.Ok(await admin.GetAsync("/api/v1/roles?pageSize=200"))).AsArray();
        var adminRole = (string)roles.First(r => (string)r!["name"]! == "Org admin")!["id"]!;
        var helpdeskRole = await RoleAsync(admin, "Helpdesk", Delegated);
        var small = await RoleAsync(admin, "Small", CorePermissions.PartiesRead);
        var big = await RoleAsync(admin, "Big", CorePermissions.PartiesRead, CorePermissions.InvoicesRead);
        var email = HrKit.Unique("helpdesk") + "@demo.local";
        var helpdeskId = (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Help Desk", temporaryPassword = Pw, roleIds = new[] { helpdeskRole } }), HttpStatusCode.Created))["id"]!;
        var (helpdesk, _) = await f.SignInAsync(email, Pw);
        return new Setup(admin, helpdesk, helpdeskId, helpdeskRole, adminRole, small, big, await InviteAsync(admin, small));
    }

    private static async Task<List<string>> PermissionsOfAsync(HttpClient c, string role)
    {
        var roles = (await HrKit.Ok(await c.GetAsync("/api/v1/roles?pageSize=200"))).AsArray();
        return roles.First(r => (string)r!["id"]! == role)!["permissions"]!.AsArray().Select(p => (string)p!).ToList();
    }

    [Fact]
    public async Task A_new_role_can_only_hold_what_the_caller_holds()
    {
        var s = await StartAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Helpdesk.PostAsJsonAsync("/api/v1/roles", new RoleInput(HrKit.Unique("Wild"), null, [CorePermissions.All]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Helpdesk.PostAsJsonAsync("/api/v1/roles", new RoleInput(HrKit.Unique("More"), null, [CorePermissions.PartiesRead, CorePermissions.InvoicesRead]))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await s.Helpdesk.PostAsJsonAsync("/api/v1/roles", new RoleInput(HrKit.Unique("Less"), null, [CorePermissions.PartiesRead]))).StatusCode);
    }

    [Fact]
    public async Task Editing_a_role_cannot_add_what_the_caller_lacks_but_may_keep_what_it_has()
    {
        var s = await StartAsync();
        // their own role: no wildcard, nothing new
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Helpdesk.PutAsJsonAsync($"/api/v1/roles/{s.HelpdeskRole}", new RoleInput(HrKit.Unique("Mine"), null, [.. Delegated, CorePermissions.All]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Helpdesk.PutAsJsonAsync($"/api/v1/roles/{s.HelpdeskRole}", new RoleInput(HrKit.Unique("Mine"), null, [.. Delegated, CorePermissions.InvoicesRead]))).StatusCode);
        Assert.DoesNotContain(CorePermissions.All, await PermissionsOfAsync(s.Admin, s.HelpdeskRole));
        Assert.DoesNotContain(CorePermissions.InvoicesRead, await PermissionsOfAsync(s.Admin, s.HelpdeskRole));

        // another role: a permission it already has is not a grant, so a rename keeps working
        await HrKit.Ok(await s.Helpdesk.PutAsJsonAsync($"/api/v1/roles/{s.BigRole}", new RoleInput(HrKit.Unique("Renamed"), null, [CorePermissions.PartiesRead, CorePermissions.InvoicesRead])));
        Assert.Contains(CorePermissions.InvoicesRead, await PermissionsOfAsync(s.Admin, s.BigRole));
    }

    [Fact]
    public async Task A_delegated_admin_cannot_change_their_own_roles_or_assign_a_bigger_role()
    {
        var s = await StartAsync();
        // self-promotion, to the wildcard role and to a role within what they hold
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Helpdesk.PutAsJsonAsync($"/api/v1/users/{s.HelpdeskId}/roles", new { roleIds = new[] { s.HelpdeskRole, s.AdminRole } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Helpdesk.PutAsJsonAsync($"/api/v1/users/{s.HelpdeskId}/roles", new { roleIds = new[] { s.HelpdeskRole, s.SmallRole } })).StatusCode);
        // sending their current roles back changes nothing and is harmless
        await HrKit.Ok(await s.Helpdesk.PutAsJsonAsync($"/api/v1/users/{s.HelpdeskId}/roles", new { roleIds = new[] { s.HelpdeskRole } }));

        // someone else: only roles within what they hold
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Helpdesk.PutAsJsonAsync($"/api/v1/users/{s.MemberId}/roles", new { roleIds = new[] { s.AdminRole } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Helpdesk.PutAsJsonAsync($"/api/v1/users/{s.MemberId}/roles", new { roleIds = new[] { s.BigRole } })).StatusCode);
        await HrKit.Ok(await s.Helpdesk.PutAsJsonAsync($"/api/v1/users/{s.MemberId}/roles", new { roleIds = new[] { s.SmallRole } }));

        var users = (await HrKit.Ok(await s.Admin.GetAsync("/api/v1/users"))).AsArray();
        var self = users.First(u => (string)u!["id"]! == s.HelpdeskId)!;
        Assert.Equal([s.HelpdeskRole], self["roleIds"]!.AsArray().Select(r => (string)r!));
    }

    [Fact]
    public async Task An_invite_cannot_give_a_bigger_role_and_a_refused_invite_creates_no_account()
    {
        var s = await StartAsync();
        var email = HrKit.Unique("refused") + "@demo.local";
        var refused = await s.Helpdesk.PostAsJsonAsync("/api/v1/users", new { email, fullName = "No One", temporaryPassword = Pw, roleIds = new[] { s.AdminRole } });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Pw })).StatusCode);

        Assert.False(string.IsNullOrEmpty(await InviteAsync(s.Helpdesk, s.SmallRole)));
    }

    [Fact]
    public async Task A_wildcard_holder_can_still_do_all_of_it()
    {
        var s = await StartAsync();
        var wild = await RoleAsync(s.Admin, "Wildcard", CorePermissions.All);
        await HrKit.Ok(await s.Admin.PutAsJsonAsync($"/api/v1/roles/{s.SmallRole}", new RoleInput(HrKit.Unique("Grown"), null, [CorePermissions.PartiesRead, CorePermissions.InvoicesRead, CorePermissions.All])));
        await HrKit.Ok(await s.Admin.PutAsJsonAsync($"/api/v1/users/{s.MemberId}/roles", new { roleIds = new[] { s.AdminRole, wild } }));
        Assert.False(string.IsNullOrEmpty(await InviteAsync(s.Admin, s.AdminRole)));

        var users = (await HrKit.Ok(await s.Admin.GetAsync("/api/v1/users"))).AsArray();
        var adminId = (string)users.First(u => (string)u!["email"]! == ApiFactory.AdminEmail)!["id"]!;
        var mine = users.First(u => (string)u!["id"]! == adminId)!["roleIds"]!.AsArray().Select(r => (string)r!).ToList();
        await HrKit.Ok(await s.Admin.PutAsJsonAsync($"/api/v1/users/{adminId}/roles", new { roleIds = mine.Append(s.BigRole).ToArray() }));
    }
}
