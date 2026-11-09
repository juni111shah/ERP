using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modularity.Features;

/// <summary>What kind of business this workspace is, which core areas it uses and its lists.</summary>
public sealed record GetBusinessProfileQuery : IQuery<BusinessProfile>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsRead; }
