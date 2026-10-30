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

/// <summary>Edits a custom role. Built-in roles (from the core or an industry template) are read-only: create a copy instead.</summary>
public sealed record UpdateRole(Guid Id, RoleInput Input) : ICommand<CreatedRoleDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.RolesWrite;
}

/// <summary>Replaces the roles of a workspace member.</summary>
public sealed record SetUserRoles(Guid UserId, Guid[] RoleIds) : ICommand<UserSummary>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.UsersWrite;
}

/// <summary>Activates or deactivates a workspace member (the account itself is kept).</summary>
public sealed record SetUserActive(Guid UserId, bool IsActive) : ICommand<UserSummary>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.UsersWrite;
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

public sealed class UpdateRoleValidator : AbstractValidator<UpdateRole>
{
    public UpdateRoleValidator()
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

internal sealed partial class SecurityHandlers(
    IPermissionCatalog catalog, IRepository<Role> roles, IUserDirectory directory, IRepository<OrganizationMember> members,
    IRepository<RolePermission> rolePermissions, IRepository<UserRole> userRoles, IRepository<UserLegalEntityAccess> legalEntityAccess, IReadRepository<LegalEntity> legalEntities,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, ERP.Application.Auth.IAuthService auth) :
    IRequestHandler<ListPermissions, IReadOnlyList<PermissionDefinition>>,
    IRequestHandler<CreateRole, CreatedRoleDto>,
    IRequestHandler<ListUsers, IReadOnlyList<UserSummary>>,
    IRequestHandler<InviteUser, UserAccount>,
    IRequestHandler<UpdateRole, CreatedRoleDto>,
    IRequestHandler<SetUserRoles, UserSummary>,
    IRequestHandler<SetUserActive, UserSummary>
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

    public async Task<CreatedRoleDto> Handle(UpdateRole cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var role = await roles.Tracked(new Specification<Role>(r => r.Id == cmd.Id)).Include(r => r.Permissions).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (role.IsBuiltIn) throw new ConflictException("Built-in roles cannot be edited. Create a custom role instead.");
        var unknown = i.Permissions.Where(p => p != CorePermissions.All && catalog.All.All(d => d.Key != p)).ToList();
        if (unknown.Count > 0) throw new FieldValidationException("permissions", $"Unknown permissions: {string.Join(", ", unknown)}");
        var name = i.Name.Trim();
        if (name != role.Name && await roles.AnyAsync(r => r.Name == name, ct)) throw new ConflictException("A role with that name exists.");
        role.Name = name; role.Description = i.Description;
        // Apply only the difference: removing and re-adding an unchanged permission would collide with its unique index.
        var wanted = i.Permissions.Distinct().ToHashSet();
        foreach (var removed in role.Permissions.Where(p => !wanted.Contains(p.Permission)).ToList()) { role.Permissions.Remove(removed); rolePermissions.Remove(removed); }
        // Explicit Add: a new child with a client-generated key attached only through the navigation would be tracked as existing (Modified).
        rolePermissions.AddRange(wanted.Where(w => role.Permissions.All(p => p.Permission != w)).Select(w => new RolePermission { RoleId = role.Id, Permission = w }));
        await roles.SaveChangesAsync(ct);
        return new(role.Id, role.Name);
    }

    public async Task<UserSummary> Handle(SetUserRoles cmd, CancellationToken ct)
    {
        var member = await members.Tracked(new Specification<OrganizationMember>(m => m.UserId == cmd.UserId)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("That user is not a member of this workspace.");
        var roleIds = await roles.Query(new Specification<Role>(r => cmd.RoleIds.Contains(r.Id))).Select(r => r.Id).ToListAsync(ct);
        var current = await userRoles.Tracked(new Specification<UserRole>(ur => ur.UserId == cmd.UserId)).ToListAsync(ct);
        var adminRoleIds = await AdminRoleIdsAsync(ct);
        var losesAdmin = current.Any(ur => adminRoleIds.Contains(ur.RoleId)) && !roleIds.Any(adminRoleIds.Contains);
        if (losesAdmin) await EnsureAnotherAdministratorAsync(cmd.UserId, adminRoleIds, ct);
        foreach (var ur in current.Where(ur => !roleIds.Contains(ur.RoleId))) userRoles.Remove(ur);
        userRoles.AddRange(roleIds.Where(id => current.All(ur => ur.RoleId != id)).Select(id => new UserRole { UserId = cmd.UserId, RoleId = id }));
        await unitOfWork.SaveChangesAsync(ct);
        return await SummaryAsync(member.UserId, ct);
    }

    public async Task<UserSummary> Handle(SetUserActive cmd, CancellationToken ct)
    {
        var member = await members.Tracked(new Specification<OrganizationMember>(m => m.UserId == cmd.UserId)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("That user is not a member of this workspace.");
        if (!cmd.IsActive)
        {
            if (cmd.UserId == currentUser.UserId) throw new ConflictException("You cannot deactivate your own account.");
            var adminRoleIds = await AdminRoleIdsAsync(ct);
            if (await userRoles.AnyAsync(ur => ur.UserId == cmd.UserId && adminRoleIds.Contains(ur.RoleId), ct)) await EnsureAnotherAdministratorAsync(cmd.UserId, adminRoleIds, ct);
        }
        member.IsActive = cmd.IsActive;
        await unitOfWork.SaveChangesAsync(ct);
        if (!cmd.IsActive) await auth.RevokeSessionsAsync(member.UserId, member.OrgId, "member-deactivated", ct);
        return await SummaryAsync(member.UserId, ct);
    }

    /// <summary>Roles that grant everything ("*"): the workspace must always keep one active holder.</summary>
    private async Task<List<Guid>> AdminRoleIdsAsync(CancellationToken ct)
        => await roles.Query(new Specification<Role>(r => r.Permissions.Any(p => p.Permission == CorePermissions.All))).Select(r => r.Id).ToListAsync(ct);

    private async Task EnsureAnotherAdministratorAsync(Guid userId, List<Guid> adminRoleIds, CancellationToken ct)
    {
        var others = await userRoles.Query(new Specification<UserRole>(ur => adminRoleIds.Contains(ur.RoleId) && ur.UserId != userId)).Select(ur => ur.UserId).Distinct().ToListAsync(ct);
        if (!await members.AnyAsync(m => others.Contains(m.UserId) && m.IsActive, ct))
            throw new ConflictException("The workspace needs at least one active administrator.");
    }

    private async Task<UserSummary> SummaryAsync(Guid userId, CancellationToken ct)
        => (await directory.ListMembersAsync(ct)).First(u => u.Id == userId);

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

