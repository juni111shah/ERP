using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Whether two-factor sign-in is on for the signed-in user.</summary>
public sealed record GetTwoFactorStatusQuery : IQuery<TwoFactorStatus>;
