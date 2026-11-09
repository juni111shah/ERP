using System.Linq.Expressions;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

public sealed record RoleInput(string Name, string? Description, string[] Permissions);

public sealed record RoleDto(Guid Id, string Name, string? Description, bool IsBuiltIn, string? ModuleKey, List<string> Permissions)
{
    public static readonly Expression<Func<Role, RoleDto>> Projection =
        r => new RoleDto(r.Id, r.Name, r.Description, r.IsBuiltIn, r.ModuleKey, r.Permissions.OrderBy(p => p.Permission).Select(p => p.Permission).ToList());
}

public sealed record CreatedRoleDto(Guid Id, string Name);
