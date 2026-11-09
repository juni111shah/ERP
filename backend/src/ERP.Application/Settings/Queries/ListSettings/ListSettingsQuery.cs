using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Settings;

/// <summary>Every setting of the core and of the enabled modules, with its current value.</summary>
public sealed record ListSettingsQuery : IQuery<IReadOnlyList<SettingView>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.SettingsRead;
}
