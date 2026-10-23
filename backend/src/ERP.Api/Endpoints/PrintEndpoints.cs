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
    }
}
