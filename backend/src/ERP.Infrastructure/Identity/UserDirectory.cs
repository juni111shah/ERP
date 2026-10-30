using ERP.Application.Security;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Identity;

/// <summary>User accounts on top of ASP.NET Identity. Tenant filters apply to memberships and roles; accounts themselves are global.</summary>
public sealed class UserDirectory(AppDbContext db, UserManager<AppUser> users) : IUserDirectory
{
    public async Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        return u is null ? null : new(u.Id, u.Email ?? "", u.FullName, u.MustChangePassword);
    }

    public Task<IReadOnlyList<UserSummary>> ListMembersAsync(CancellationToken ct = default) => MembersAsync(null, ct);

    public Task<IReadOnlyList<UserSummary>> ListMembersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) => MembersAsync(userIds, ct);

    /// <summary>One pass per table with lookups (no per-account rescans); optionally limited to some users.</summary>
    private async Task<IReadOnlyList<UserSummary>> MembersAsync(IReadOnlyCollection<Guid>? only, CancellationToken ct)
    {
        var members = await db.OrganizationMembers.AsNoTracking().Where(m => only == null || only.Contains(m.UserId)).Select(m => new { m.UserId, m.IsActive }).ToListAsync(ct);
        var active = members.GroupBy(m => m.UserId).ToDictionary(g => g.Key, g => g.First().IsActive);
        var ids = active.Keys.ToList();
        var accounts = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.Email, u.FullName, u.IsActive, u.LastSignInAt }).ToListAsync(ct);
        var roles = (await db.UserRoles.AsNoTracking().Where(r => ids.Contains(r.UserId)).Select(r => new { r.UserId, r.RoleId, r.Role!.Name }).ToListAsync(ct)).ToLookup(r => r.UserId);
        return accounts.OrderBy(a => a.FullName).Select(a => new UserSummary(
            a.Id, a.Email ?? "", a.FullName, a.IsActive && active[a.Id], a.LastSignInAt,
            roles[a.Id].Select(r => r.Name).ToList(),
            roles[a.Id].Select(r => r.RoleId).ToList())).ToList();
    }

    public async Task<UserAccount> EnsureAccountAsync(string email, string fullName, string? temporaryPassword, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(temporaryPassword)) throw new FieldValidationException("temporaryPassword", "A temporary password is required for new accounts.");
            user = new AppUser
            {
                Id = ERP.Domain.Common.SequentialGuid.NewGuid(), Email = email, UserName = email, FullName = fullName,
                CreatedAt = DateTime.UtcNow, MustChangePassword = true,
            };
            var created = await users.CreateAsync(user, temporaryPassword);
            if (!created.Succeeded)
                throw new FieldValidationException(new Dictionary<string, string[]> { ["temporaryPassword"] = created.Errors.Select(e => e.Description).ToArray() });
        }
        return new(user.Id, user.Email ?? email, user.FullName);
    }
}
