using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The chart-of-accounts numbering mask of the workspace (the default when none is set).</summary>
public sealed record GetCoaNumberingQuery : IQuery<CoaNumberingDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsRead;
}
