using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Whether a workspace code is free to register.</summary>
public sealed record CheckSignInKeyQuery(string Key) : IQuery<SignInKeyAvailability>;
