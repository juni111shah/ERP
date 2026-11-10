using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>
/// Drafts a credit note against an issued invoice: same customer and entity, the selected lines (all that is still uncredited when none
/// are given), linked through SourceType "invoice" + SourceId and CreditNoteForId. The invoice's non-voided credit notes together cannot
/// exceed the invoice total (see <see cref="InvoiceCreditCaps"/>).
/// </summary>
public sealed record CreateCreditNoteFromInvoiceCommand(Guid InvoiceId, DateOnly? Date, List<CreditNoteLineInput>? Lines, string? Notes)
    : ICommand<InvoiceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesCredit;
}
