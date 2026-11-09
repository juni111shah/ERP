using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Deletes a draft invoice. The record it was raised from (milestone, sales order...) becomes billable again.</summary>
public sealed record DeleteInvoiceCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesWrite;
}
