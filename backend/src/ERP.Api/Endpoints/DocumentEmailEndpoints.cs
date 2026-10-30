using ERP.Application.Printing;
using ERP.Application.Security;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

/// <summary>Email an issued document or a statement to its party (needs `core.email.send` plus the document's own read permission).</summary>
public static class DocumentEmailEndpoints
{
    public static void MapDocumentEmail(this RouteGroupBuilder api)
    {
        void Doc(string path, EmailDocKind kind) =>
            api.MapPost(path, (Guid id, EmailDocumentInput? body, ISender s, CancellationToken ct) => s.Send(new EmailDocument(kind, id, body ?? new(null, null)), ct))
               .WithTags("Email").RequirePermission(CorePermissions.EmailSend);

        Doc("/finance/invoices/{id:guid}/email", EmailDocKind.Invoice);
        Doc("/finance/receipts/{id:guid}/email", EmailDocKind.Receipt);
        Doc("/finance/bills/{id:guid}/email", EmailDocKind.Bill);
        Doc("/finance/payments/{id:guid}/email", EmailDocKind.Payment);
        api.MapPost("/parties/{id:guid}/statement/email", (Guid id, DateOnly? asOf, EmailDocumentInput? body, ISender s, CancellationToken ct) => s.Send(new EmailPartyStatement(id, asOf, body ?? new(null, null)), ct))
           .WithTags("Email").RequirePermission(CorePermissions.EmailSend);
    }
}
