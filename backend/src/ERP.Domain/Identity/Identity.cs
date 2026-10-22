using ERP.Domain.Common;

namespace ERP.Domain.Identity;

/// <summary>A tenant role. Permissions are resource.action keys (e.g. "parties.write").</summary>
public class Role : OrgEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    /// <summary>Built-in roles come from the core or an industry template and cannot be deleted.</summary>
    public bool IsBuiltIn { get; set; }
    /// <summary>Owning module for template roles (null = core).</summary>
    public string? ModuleKey { get; set; }
    public List<RolePermission> Permissions { get; set; } = new();
}

public class RolePermission : Entity
{
    public Guid RoleId { get; set; }
    public string Permission { get; set; } = default!;
}

public class UserRole : Entity, IOrgScoped
{
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}

/// <summary>Legal entities a user may work in. No rows = all entities of the tenant.</summary>
public class UserLegalEntityAccess : Entity, IOrgScoped
{
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public Guid LegalEntityId { get; set; }
}

/// <summary>Rotating refresh token. Only a SHA-256 hash is stored. Tokens of one login share a FamilyId for reuse detection.</summary>
public class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public Guid OrgId { get; set; }
    public string TokenHash { get; set; } = default!;
    public Guid FamilyId { get; set; }
    public bool IsPersistent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? ReplacedById { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
    public bool IsActive(DateTime now) => RevokedAt is null && ReplacedById is null && ExpiresAt > now;
}

/// <summary>A user's membership of a workspace (organization). One login (email) can belong to several workspaces.</summary>
public class OrganizationMember : Entity, IOrgScoped
{
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; }
    public DateTime? LastSignInAt { get; set; }
}

/// <summary>Self-service password reset token: hashed, single use, 30-minute lifetime.</summary>
public class PasswordResetToken : Entity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
