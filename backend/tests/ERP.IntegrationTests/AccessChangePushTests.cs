using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.IntegrationTests;

/// <summary>Records what would go over SignalR, instead of sending it.</summary>
public sealed class CapturingPusher : INotificationPusher
{
    public ConcurrentQueue<(Guid UserId, string Event, string Payload)> Sent { get; } = new();
    public Task PushAsync(Guid userId, object payload, CancellationToken ct = default) => SendAsync(userId, "notification", payload, ct);
    public Task SendAsync(Guid userId, string eventName, object payload, CancellationToken ct = default)
    {
        Sent.Enqueue((userId, eventName, JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        return Task.CompletedTask;
    }

    public List<string> AccessChangesFor(string userId)
        => Sent.Where(s => s.Event == "access-changed" && s.UserId == Guid.Parse(userId)).Select(s => s.Payload).ToList();
}

/// <summary>The API host with the SignalR pusher replaced by <see cref="CapturingPusher"/>.</summary>
public sealed class PushCaptureFactory : ApiFactory
{
    public CapturingPusher Pushes { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<INotificationPusher>();
            s.AddSingleton<INotificationPusher>(Pushes);
        });
    }
}

/// <summary>An administrator's change to roles, role assignments or modules reaches the affected people's open screens at once.</summary>
public class AccessChangePushTests(PushCaptureFactory f) : IClassFixture<PushCaptureFactory>
{
    private async Task<(HttpClient Admin, string RoleId, string UserId, string OrgId)> MemberAsync(params string[] permissions)
    {
        var (admin, session) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var role = (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/roles", new { name = "Push " + Guid.NewGuid().ToString("N")[..6], description = "test", permissions }), HttpStatusCode.Created))["id"]!;
        var user = await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/users", new { email = $"push_{Guid.NewGuid():N}@demo.local", fullName = "Push User", temporaryPassword = HrKit.Password, roleIds = new[] { role } }), HttpStatusCode.Created);
        var me = await ApiFactory.Json(await admin.GetAsync("/api/v1/me"));
        return (admin, role, (string)user["id"]!, (string)me["workspace"]!["id"]!);
    }

    [Fact]
    public async Task Editing_a_role_tells_everyone_who_holds_it()
    {
        var (admin, role, user, org) = await MemberAsync(CorePermissions.PartiesRead);
        await HrKit.Ok(await admin.PutAsJsonAsync($"/api/v1/roles/{role}", new { name = "Push edited " + Guid.NewGuid().ToString("N")[..6], description = "test", permissions = new[] { CorePermissions.ItemsRead } }));

        var pushed = Assert.Single(f.Pushes.AccessChangesFor(user));
        Assert.Contains("\"reason\":\"role-changed\"", pushed);
        Assert.Contains(org, pushed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Changing_a_members_roles_tells_that_member()
    {
        var (admin, _, user, _) = await MemberAsync(CorePermissions.PartiesRead);
        await HrKit.Ok(await admin.PutAsJsonAsync($"/api/v1/users/{user}/roles", new { roleIds = Array.Empty<string>() }));
        Assert.Contains(f.Pushes.AccessChangesFor(user), p => p.Contains("\"reason\":\"roles-assigned\""));
    }

    [Fact]
    public async Task Switching_a_module_tells_every_member_of_the_workspace()
    {
        var (admin, _, user, _) = await MemberAsync(CorePermissions.PartiesRead);
        var before = f.Pushes.AccessChangesFor(user).Count;
        var modules = (await ApiFactory.Json(await admin.GetAsync("/api/v1/modules"))).AsArray();
        // A module nothing else depends on, so it can be switched off and on again.
        var keys = modules.Select(m => (string)m!["key"]!).ToList();
        var key = keys.Contains("assets") ? "assets" : "crm";
        var on = (string?)modules.First(m => (string)m!["key"]! == key)!["state"] == "enabled";
        await HrKit.Ok(await admin.PostAsync($"/api/v1/modules/{key}/{(on ? "disable" : "enable")}", null));
        await HrKit.Ok(await admin.PostAsync($"/api/v1/modules/{key}/{(on ? "enable" : "disable")}", null));

        var after = f.Pushes.AccessChangesFor(user);
        Assert.True(after.Count >= before + 2, $"Expected two pushes for the module switches, got {after.Count - before}");
        Assert.Contains(after, p => p.Contains("\"reason\":\"modules-changed\""));
    }
}
