using ERP.Application.Abstractions;
using ERP.Application.Common;
using FluentValidation;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Security;

public sealed record RoleInput(string Name, string? Description, string[] Permissions);
public sealed record RoleDto(Guid Id, string Name, string? Description, bool IsBuiltIn, string? ModuleKey, List<string> Permissions);
public sealed record CreatedRoleDto(Guid Id, string Name);

public sealed record InviteUserInput(string Email, string FullName, string? TemporaryPassword, Guid[] RoleIds, Guid[]? LegalEntityIds);

public sealed record ListPermissions : IQuery<IReadOnlyList<PermissionDefinition>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.RolesRead;
}

public sealed record CreateRole(RoleInput Input) : ICommand<CreatedRoleDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.RolesWrite;
}

public sealed record ListUsers : IQuery<IReadOnlyList<UserSummary>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.UsersRead;
}

public sealed record InviteUser(InviteUserInput Input) : ICommand<UserAccount>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.UsersWrite;
}

public sealed class CreateRoleValidator : AbstractValidator<CreateRole>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(100).WithMessage("Name is required.");
        RuleFor(x => x.Input.Permissions).NotNull();
    }
}

public sealed class InviteUserValidator : AbstractValidator<InviteUser>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.Input.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Input.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Input.RoleIds).NotNull();
    }
}

internal sealed class SecurityHandlers(
    IPermissionCatalog catalog, IRepository<Role> roles, IUserDirectory directory, IRepository<OrganizationMember> members,
    IRepository<UserRole> userRoles, IRepository<UserLegalEntityAccess> legalEntityAccess, IReadRepository<LegalEntity> legalEntities,
    IUnitOfWork unitOfWork) :
    IRequestHandler<ListPermissions, IReadOnlyList<PermissionDefinition>>,
    IRequestHandler<CreateRole, CreatedRoleDto>,
    IRequestHandler<ListUsers, IReadOnlyList<UserSummary>>,
    IRequestHandler<InviteUser, UserAccount>
{
    public Task<IReadOnlyList<PermissionDefinition>> Handle(ListPermissions q, CancellationToken ct) => Task.FromResult(catalog.All);

    public async Task<CreatedRoleDto> Handle(CreateRole cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var unknown = i.Permissions.Where(p => p != CorePermissions.All && catalog.All.All(d => d.Key != p)).ToList();
        if (unknown.Count > 0) throw new FieldValidationException("permissions", $"Unknown permissions: {string.Join(", ", unknown)}");
        var name = i.Name.Trim();
        if (await roles.AnyAsync(r => r.Name == name, ct)) throw new ConflictException("A role with that name exists.");
        var role = new Role { Name = name, Description = i.Description };
        role.Permissions.AddRange(i.Permissions.Distinct().Select(p => new RolePermission { Permission = p }));
        roles.Add(role);
        await roles.SaveChangesAsync(ct);
        return new(role.Id, role.Name);
    }

    public Task<IReadOnlyList<UserSummary>> Handle(ListUsers q, CancellationToken ct) => directory.ListMembersAsync(ct);

    public async Task<UserAccount> Handle(InviteUser cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var account = await directory.EnsureAccountAsync(i.Email.Trim(), i.FullName.Trim(), i.TemporaryPassword, ct);
        if (await members.AnyAsync(m => m.UserId == account.Id, ct)) throw new ConflictException("That user is already a member of this workspace.");

        // Only roles / legal entities of this workspace count (the repositories apply the tenant filter).
        var roleIds = await roles.Query(new Specification<Role>(r => i.RoleIds.Contains(r.Id))).Select(r => r.Id).ToListAsync(ct);
        members.Add(new OrganizationMember { UserId = account.Id, JoinedAt = DateTime.UtcNow });
        userRoles.AddRange(roleIds.Select(r => new UserRole { UserId = account.Id, RoleId = r }));
        if (i.LegalEntityIds is { Length: > 0 })
        {
            var entityIds = await legalEntities.Query(new Specification<LegalEntity>(e => i.LegalEntityIds.Contains(e.Id))).Select(e => e.Id).ToListAsync(ct);
            legalEntityAccess.AddRange(entityIds.Select(e => new UserLegalEntityAccess { UserId = account.Id, LegalEntityId = e }));
        }
        await unitOfWork.SaveChangesAsync(ct);
        return account;
    }
}
