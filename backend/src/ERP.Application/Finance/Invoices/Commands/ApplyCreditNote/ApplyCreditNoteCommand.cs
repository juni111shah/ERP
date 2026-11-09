using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Uses (part of) an issued credit note to settle an open invoice of the same customer.</summary>
public sealed record ApplyCreditNoteCommand(Guid CreditNoteId, Guid InvoiceId, decimal Amount) : ICommand<InvoiceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesCredit;
}
