using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Printing;

/// <summary>Get print data for a receipt.</summary>
public sealed record GetPrintReceiptQuery(Guid Id) : IQuery<PrintReceiptDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReceiptsRead;
}
