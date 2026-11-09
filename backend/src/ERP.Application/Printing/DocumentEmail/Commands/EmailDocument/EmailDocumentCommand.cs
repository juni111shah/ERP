using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Printing;

/// <summary>Emails an issued invoice, receipt, bill or payment (remittance) to its party, rendered from the same data as the print page.</summary>
public sealed record EmailDocumentCommand(EmailDocKind Kind, Guid Id, EmailDocumentInput Input) : ICommand<EmailSentDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EmailSend;
}
