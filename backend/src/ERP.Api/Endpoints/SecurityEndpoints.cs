using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Domain.Platform;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record UserRolesBody(Guid[] RoleIds);
public sealed record UserActiveBody(bool IsActive);

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

        roles.MapPut("/{id:guid}", (Guid id, RoleInput input, ISender sender, CancellationToken ct) => sender.Send(new UpdateRole(id, input), ct)).RequirePermission(CorePermissions.RolesWrite);

        var users = api.MapGroup("/users").WithTags("Security");
        users.MapGet("", (ISender sender, CancellationToken ct) => sender.Send(new ListUsers(), ct)).RequirePermission(CorePermissions.UsersRead);
        users.MapPost("", async (InviteUserInput input, ISender sender, CancellationToken ct) =>
        {
            var invited = await sender.Send(new InviteUser(input), ct);
            return Results.Created($"/api/v1/users/{invited.Id}", invited);
        }).RequirePermission(CorePermissions.UsersWrite);

        users.MapPut("/{id:guid}/roles", (Guid id, UserRolesBody body, ISender sender, CancellationToken ct) => sender.Send(new SetUserRoles(id, body.RoleIds), ct)).RequirePermission(CorePermissions.UsersWrite);
        users.MapPost("/{id:guid}/active", (Guid id, UserActiveBody body, ISender sender, CancellationToken ct) => sender.Send(new SetUserActive(id, body.IsActive), ct)).RequirePermission(CorePermissions.UsersWrite);

        // ?filter=recordType:eq:Party&filter=recordId:eq:{id}&sort=-occurredAt
        api.MapGet("/audit/record", (string recordType, string recordId, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
            sender.Send(new ERP.Application.Audit.GetRecordHistory(recordType, recordId, page ?? 1, pageSize ?? 25), ct)).RequirePermission(CorePermissions.AuditRead).WithTags("Security");
        api.MapPagedList<AuditEvent, AuditEvent>("/audit", CorePermissions.AuditRead, a => a).WithTags("Security");
    }
}
