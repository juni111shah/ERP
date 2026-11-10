using ERP.Application.Printing;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Documents;

/// <summary>Print data of invoices, receipts, bills, payments and journals, and the same documents as PDF files.</summary>
[Route("api/v1/print")]
public sealed class PrintController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("invoice/{id:guid}")]
    [RequirePermission(CorePermissions.InvoicesRead)]
    public Task<PrintInvoiceDto> Invoice(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPrintInvoiceQuery(id), ct);

    [HttpGet("receipt/{id:guid}")]
    [RequirePermission(CorePermissions.ReceiptsRead)]
    public Task<PrintReceiptDto> Receipt(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPrintReceiptQuery(id), ct);

    [HttpGet("bill/{id:guid}")]
    [RequirePermission(CorePermissions.BillsRead)]
    public Task<PrintBillDto> Bill(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPrintBillQuery(id), ct);

    [HttpGet("payment/{id:guid}")]
    [RequirePermission(CorePermissions.PaymentsRead)]
    public Task<PrintPaymentDto> Payment(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPrintPaymentQuery(id), ct);

    [HttpGet("journal/{id:guid}")]
    [RequirePermission(CorePermissions.JournalsRead)]
    public Task<PrintJournalDto> Journal(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPrintJournalQuery(id), ct);

    // The same documents as real PDF files (rendered on the server, same permission and 404 rules as the print data above).

    [HttpGet("invoice/{id:guid}/pdf")]
    [RequirePermission(CorePermissions.InvoicesRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, PrintPdfFile.ContentType)]
    public Task<IActionResult> InvoicePdf(Guid id, CancellationToken ct) => Pdf(PrintDocKind.Invoice, id, ct);

    [HttpGet("receipt/{id:guid}/pdf")]
    [RequirePermission(CorePermissions.ReceiptsRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, PrintPdfFile.ContentType)]
    public Task<IActionResult> ReceiptPdf(Guid id, CancellationToken ct) => Pdf(PrintDocKind.Receipt, id, ct);

    [HttpGet("bill/{id:guid}/pdf")]
    [RequirePermission(CorePermissions.BillsRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, PrintPdfFile.ContentType)]
    public Task<IActionResult> BillPdf(Guid id, CancellationToken ct) => Pdf(PrintDocKind.Bill, id, ct);

    [HttpGet("payment/{id:guid}/pdf")]
    [RequirePermission(CorePermissions.PaymentsRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, PrintPdfFile.ContentType)]
    public Task<IActionResult> PaymentPdf(Guid id, CancellationToken ct) => Pdf(PrintDocKind.Payment, id, ct);

    [HttpGet("journal/{id:guid}/pdf")]
    [RequirePermission(CorePermissions.JournalsRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, PrintPdfFile.ContentType)]
    public Task<IActionResult> JournalPdf(Guid id, CancellationToken ct) => Pdf(PrintDocKind.Journal, id, ct);

    private async Task<IActionResult> Pdf(PrintDocKind kind, Guid id, CancellationToken ct)
    {
        var file = await Mediator.Send(new GetPrintPdfQuery(kind, id), ct);
        return File(file.Content, PrintPdfFile.ContentType, file.FileName);
    }
}
