using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modularity.Features;

/// <summary>Enables or disables an installed module for the current workspace. Module data is never touched.</summary>
public sealed record SetModuleStateCommand(string Key, bool Enable) : ICommand<ModuleState>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ModulesManage;
}
