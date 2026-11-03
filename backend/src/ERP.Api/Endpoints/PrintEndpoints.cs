using ERP.Application.Printing;
using ERP.Application.Security;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public static class PrintEndpoints
{
    public static void MapPrint(this RouteGroupBuilder api)
    {
        api.MapGet("/print/invoice/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintInvoice(id), ct))
            .RequirePermission(CorePermissions.InvoicesRead).WithTags("Print");
        
        api.MapGet("/print/receipt/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintReceipt(id), ct))
            .RequirePermission(CorePermissions.ReceiptsRead).WithTags("Print");
        
        api.MapGet("/print/bill/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintBill(id), ct))
            .RequirePermission(CorePermissions.BillsRead).WithTags("Print");
        
        api.MapGet("/print/payment/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintPayment(id), ct))
            .RequirePermission(CorePermissions.PaymentsRead).WithTags("Print");
        
        api.MapGet("/print/journal/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintJournal(id), ct))
            .RequirePermission(CorePermissions.JournalsRead).WithTags("Print");

        // The same documents as real PDF files (rendered on the server, same permission and 404 rules as the print data above).
        MapPdf(api, "invoice", PrintDocKind.Invoice, CorePermissions.InvoicesRead);
        MapPdf(api, "receipt", PrintDocKind.Receipt, CorePermissions.ReceiptsRead);
        MapPdf(api, "bill", PrintDocKind.Bill, CorePermissions.BillsRead);
        MapPdf(api, "payment", PrintDocKind.Payment, CorePermissions.PaymentsRead);
        MapPdf(api, "journal", PrintDocKind.Journal, CorePermissions.JournalsRead);
    }

    private static void MapPdf(RouteGroupBuilder api, string kind, PrintDocKind docKind, string permission) =>
        api.MapGet($"/print/{kind}/{{id:guid}}/pdf", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var file = await sender.Send(new GetPrintPdf(docKind, id), ct);
                return Results.File(file.Content, PrintPdfFile.ContentType, file.FileName);
            })
            .RequirePermission(permission).WithTags("Print")
            .Produces(StatusCodes.Status200OK, contentType: PrintPdfFile.ContentType);
}
