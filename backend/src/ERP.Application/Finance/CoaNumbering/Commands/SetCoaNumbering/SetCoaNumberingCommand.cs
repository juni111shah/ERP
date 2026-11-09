using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Sets the chart-of-accounts numbering mask of the workspace.</summary>
public sealed record SetCoaNumberingCommand(string Mode, IReadOnlyList<int> Segments, string Separator) : ICommand<CoaNumberingDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.SettingsWrite;
}
