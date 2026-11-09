using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Printing;

/// <summary>Emails the party's open-items statement (customer statement for customers, vendor statement otherwise).</summary>
public sealed record EmailPartyStatementCommand(Guid PartyId, DateOnly? AsOf, EmailDocumentInput Input) : ICommand<EmailSentDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EmailSend;
}
