using Microsoft.AspNetCore.Identity;

namespace ERP.Infrastructure.Identity;

/// <summary>Rejects passwords equal to the email or user name (case-insensitive).</summary>
public sealed class PasswordNotEmailValidator : IPasswordValidator<AppUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        if (password is not null && (string.Equals(password, user.Email, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(password, user.UserName, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "PasswordEqualsEmail", Description = "The password cannot be the same as your email address." }));
        return Task.FromResult(IdentityResult.Success);
    }
}
