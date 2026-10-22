using Microsoft.AspNetCore.Identity;

namespace ERP.Infrastructure.Identity;

/// <summary>A login (email). Workspace membership is in OrganizationMembers, so one user can belong to several workspaces.</summary>
public class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSignInAt { get; set; }
}
