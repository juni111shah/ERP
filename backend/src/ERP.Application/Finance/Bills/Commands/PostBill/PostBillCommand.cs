using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Posts the bill (Dr expenses and input tax, Cr payables). When an approval policy covers the amount the bill waits for a decision first.</summary>
public sealed record PostBillCommand(Guid Id) : ICommand<BillDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillsPost;
}
