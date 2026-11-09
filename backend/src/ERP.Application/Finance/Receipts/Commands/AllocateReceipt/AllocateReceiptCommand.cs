using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Applies what is still unallocated on a posted receipt to open invoices of the customer.</summary>
public sealed record AllocateReceiptCommand(Guid Id, List<AllocationInput> Allocations) : ICommand<ReceiptDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReceiptsWrite;
}
