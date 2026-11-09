using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a rate card.</summary>
public sealed record DeleteRateCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.RatesManage;
}
