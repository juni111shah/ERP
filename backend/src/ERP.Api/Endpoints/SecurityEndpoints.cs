using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Domain.Platform;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public static class SecurityEndpoints
{
    public static void MapSecurity(this RouteGroupBuilder api)
    {
        api.MapGet("/permissions", (ISender sender, CancellationToken ct) => sender.Send(new ListPermissions(), ct)).RequirePermission(CorePermissions.RolesRead).WithTags("Security");

        var roles = api.MapGroup("/roles").WithTags("Security");
        roles.MapPagedList<Role, RoleDto>("", CorePermissions.RolesRead,
            r => new RoleDto(r.Id, r.Name, r.Description, r.IsBuiltIn, r.ModuleKey, r.Permissions.OrderBy(p => p.Permission).Select(p => p.Permission).ToList()));
        roles.MapPost("", async (RoleInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateRole(input), ct);
            return Results.Created($"/api/v1/roles/{created.Id}", created);
        }).RequirePermission(CorePermissions.RolesWrite);

        var users = api.MapGroup("/users").WithTags("Security");
        users.MapGet("", (ISender sender, CancellationToken ct) => sender.Send(new ListUsers(), ct)).RequirePermission(CorePermissions.UsersRead);
        users.MapPost("", async (InviteUserInput input, ISender sender, CancellationToken ct) =>
        {
            var invited = await sender.Send(new InviteUser(input), ct);
            return Results.Created($"/api/v1/users/{invited.Id}", invited);
        }).RequirePermission(CorePermissions.UsersWrite);

        // ?filter=recordType:eq:Party&filter=recordId:eq:{id}&sort=-occurredAt
        api.MapPagedList<AuditEvent, AuditEvent>("/audit", CorePermissions.AuditRead, a => a).WithTags("Security");
    }
}
