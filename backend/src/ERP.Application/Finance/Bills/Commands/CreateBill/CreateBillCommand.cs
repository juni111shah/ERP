using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Records a draft vendor bill.</summary>
public sealed record CreateBillCommand(BillInput Input) : ICommand<BillDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillsWrite;
}
