using ERP.Application.Printing;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Documents;

/// <summary>Email an issued document or a statement to its party (needs `core.email.send` plus the document's own read permission).</summary>
[Route("api/v1")]
public sealed class DocumentEmailController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpPost("finance/invoices/{id:guid}/email")]
    [RequirePermission(CorePermissions.EmailSend)]
    public Task<EmailSentDto> EmailInvoice(Guid id, EmailDocumentInput? body, CancellationToken ct) => Email(EmailDocKind.Invoice, id, body, ct);

    [HttpPost("finance/receipts/{id:guid}/email")]
    [RequirePermission(CorePermissions.EmailSend)]
    public Task<EmailSentDto> EmailReceipt(Guid id, EmailDocumentInput? body, CancellationToken ct) => Email(EmailDocKind.Receipt, id, body, ct);

    [HttpPost("finance/bills/{id:guid}/email")]
    [RequirePermission(CorePermissions.EmailSend)]
    public Task<EmailSentDto> EmailBill(Guid id, EmailDocumentInput? body, CancellationToken ct) => Email(EmailDocKind.Bill, id, body, ct);

    [HttpPost("finance/payments/{id:guid}/email")]
    [RequirePermission(CorePermissions.EmailSend)]
    public Task<EmailSentDto> EmailPayment(Guid id, EmailDocumentInput? body, CancellationToken ct) => Email(EmailDocKind.Payment, id, body, ct);

    [HttpPost("parties/{id:guid}/statement/email")]
    [RequirePermission(CorePermissions.EmailSend)]
    public Task<EmailSentDto> EmailStatement(Guid id, DateOnly? asOf, EmailDocumentInput? body, CancellationToken ct)
        => Mediator.Send(new EmailPartyStatementCommand(id, asOf, body ?? new(null, null)), ct);

    private Task<EmailSentDto> Email(EmailDocKind kind, Guid id, EmailDocumentInput? body, CancellationToken ct)
        => Mediator.Send(new EmailDocumentCommand(kind, id, body ?? new(null, null)), ct);
}
