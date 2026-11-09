using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modularity.Features;

/// <summary>Installed and planned modules with their state in the current workspace.</summary>
public sealed record ListModulesQuery : IQuery<IReadOnlyList<ModuleView>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.SettingsRead;
}
