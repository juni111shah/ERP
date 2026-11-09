using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Printing;

/// <summary>Get print data for a bill.</summary>
public sealed record GetPrintBillQuery(Guid Id) : IQuery<PrintBillDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillsRead;
}
