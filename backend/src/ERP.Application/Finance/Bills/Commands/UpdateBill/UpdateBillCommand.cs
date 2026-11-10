using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Replaces the header and lines of a draft bill (or one waiting for approval, which then goes back to draft).</summary>
public sealed record UpdateBillCommand(Guid Id, BillInput Input) : ICommand<BillDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillsWrite;
}
