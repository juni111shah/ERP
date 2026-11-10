using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The platform's own settings: registration, billing, company details and legal texts.</summary>
public sealed record GetPlatformSettingsQuery : IQuery<PlatformSettingsView>;
