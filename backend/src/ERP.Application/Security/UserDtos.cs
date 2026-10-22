namespace ERP.Application.Security;

public sealed record UserSummary(Guid Id, string Email, string FullName, bool IsActive, DateTime? LastSignInAt, IReadOnlyList<string> Roles, IReadOnlyList<Guid> RoleIds);
public sealed record UserAccount(Guid Id, string Email, string FullName);
public sealed record UserProfile(Guid Id, string Email, string FullName, bool MustChangePassword);
