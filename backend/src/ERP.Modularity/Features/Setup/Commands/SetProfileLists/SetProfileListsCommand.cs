using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modularity.Features;

/// <summary>Replaces the workspace's units and item categories (a list left out is kept). Returns the updated profile.</summary>
public sealed record SetProfileListsCommand(ProfileListsInput Input) : ICommand<BusinessProfile>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
