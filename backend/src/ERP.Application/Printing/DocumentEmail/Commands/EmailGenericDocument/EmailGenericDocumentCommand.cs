using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Printing;

/// <summary>Emails a prepared document (see <see cref="PrintGenericDto"/>) to <c>To</c>. The caller has already checked its own module permission and the document's state.</summary>
public sealed record EmailGenericDocumentCommand(string Kind, string Id, PrintGenericDto Doc, string To, string? Message) : ICommand<EmailSentDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EmailSend;
}
