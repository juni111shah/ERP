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

    public async Task<IReadOnlyList<UserSummary>> ListMembersAsync(CancellationToken ct = default)
    {
        var members = await db.OrganizationMembers.AsNoTracking().ToListAsync(ct);
        var ids = members.Select(m => m.UserId).ToList();
        var accounts = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(ct);
        var userRoles = await db.UserRoles.AsNoTracking().Include(r => r.Role).ToListAsync(ct);
        return accounts.OrderBy(a => a.FullName).Select(a => new UserSummary(
            a.Id, a.Email ?? "", a.FullName, a.IsActive && members.First(m => m.UserId == a.Id).IsActive, a.LastSignInAt,
            userRoles.Where(r => r.UserId == a.Id).Select(r => r.Role!.Name).ToList(),
            userRoles.Where(r => r.UserId == a.Id).Select(r => r.RoleId).ToList())).ToList();
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
