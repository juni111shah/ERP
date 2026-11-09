using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Deletes a draft bill (or one waiting for approval, withdrawing the request) and releases the documents it was raised from.</summary>
public sealed record DeleteBillCommand(Guid Id) : ICommand, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillsWrite;
}
