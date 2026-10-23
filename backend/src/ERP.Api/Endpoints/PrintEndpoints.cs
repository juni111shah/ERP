using ERP.Application.Printing;
using MediatR;

namespace ERP.Api.Endpoints;

public static class PrintEndpoints
{
    public static void MapPrint(this RouteGroupBuilder api)
    {
        api.MapGet("/print/invoice/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintInvoice(id), ct))
            .RequireAuthorization().WithTags("Print");
        
        api.MapGet("/print/receipt/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintReceipt(id), ct))
            .RequireAuthorization().WithTags("Print");
        
        api.MapGet("/print/bill/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintBill(id), ct))
            .RequireAuthorization().WithTags("Print");
        
        api.MapGet("/print/payment/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintPayment(id), ct))
            .RequireAuthorization().WithTags("Print");
        
        api.MapGet("/print/journal/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetPrintJournal(id), ct))
            .RequireAuthorization().WithTags("Print");
    }
}
