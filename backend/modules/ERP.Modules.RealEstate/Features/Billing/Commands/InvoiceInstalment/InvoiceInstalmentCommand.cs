using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Invoices one instalment ahead of its due date.</summary>
public sealed record InvoiceInstalmentCommand(Guid InstalmentId) : ICommand<InstalmentInvoiceDto>, IRequirePermission
{
    public string RequiredPermission => P.BillingRun;
}
