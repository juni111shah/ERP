using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.IndustryPacks;

public sealed record RecordTransitionBody(string To, string? Reason);
public sealed record RecordBillingBody(DateOnly? Date, Guid? TaxCodeId, IReadOnlyList<Guid>? LineIds, string? VendorReference);
public sealed record RecordConsumeBody(DateOnly? Date, Guid? WarehouseId);
public sealed record RecordProgressBody(IReadOnlyList<RecordProgressLine> Lines);

/// <summary>
/// The same routes for every pack, under /api/v1/m/{key}, with the record type as a route value. One set per module keeps start-up light:
/// a set per type would compile hundreds of handlers. The permission of the type (read, manage, approve) is checked on every request in
/// the pipeline (<see cref="RecordRequest.RequiredPermission"/>), and an unknown type answers 404.
/// </summary>
internal static class PackEndpoints
{
    public static void Map(RouteGroupBuilder group, PackModule pack)
    {
        var key = pack.Key;
        group.MapGet("/definitions", (ISender s, CancellationToken ct) => s.Send(new GetPackView(key), ct)).RequireAuthorization();
        group.MapGet("/summary", (Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetRecordSummary(key, legalEntityId), ct)).RequireAuthorization();

        var g = group.MapGroup("/records/{type}").RequireAuthorization();
        g.MapGet("", (string type, [AsParameters] PagedQuery q, ISender s, CancellationToken ct) => s.Send(new ListRecords(key, Known(key, type), q), ct));
        g.MapGet("/{id:guid}", (string type, Guid id, ISender s, CancellationToken ct) => s.Send(new GetRecord(key, Known(key, type), id), ct));
        g.MapPost("", async (string type, RecordInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateRecord(key, Known(key, type), input), ct);
            return Results.Created($"/api/v1/m/{key}/records/{type}/{created.Id}", created);
        });
        g.MapPut("/{id:guid}", (string type, Guid id, RecordInput input, ISender s, CancellationToken ct) => s.Send(new UpdateRecord(key, Known(key, type), id, input), ct));
        g.MapDelete("/{id:guid}", async (string type, Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteRecord(key, Known(key, type), id), ct);
            return Results.NoContent();
        });
        g.MapPost("/{id:guid}/transition", (string type, Guid id, RecordTransitionBody body, ISender s, CancellationToken ct) => s.Send(new TransitionRecord(key, Known(key, type), id, body.To, body.Reason), ct));
        g.MapPost("/{id:guid}/invoice", (string type, Guid id, RecordBillingBody body, ISender s, CancellationToken ct) => s.Send(new InvoiceRecord(key, Known(key, type), id, body.Date, body.TaxCodeId, body.LineIds), ct));
        g.MapPost("/{id:guid}/bill", (string type, Guid id, RecordBillingBody body, ISender s, CancellationToken ct) => s.Send(new BillRecord(key, Known(key, type), id, body.Date, body.TaxCodeId, body.VendorReference), ct));
        g.MapPost("/{id:guid}/consume", (string type, Guid id, RecordConsumeBody body, ISender s, CancellationToken ct) => s.Send(new ConsumeRecord(key, Known(key, type), id, body.Date, body.WarehouseId), ct));
        g.MapPost("/{id:guid}/progress", (string type, Guid id, RecordProgressBody body, ISender s, CancellationToken ct) => s.Send(new UpdateRecordProgress(key, Known(key, type), id, body.Lines), ct));
    }

    /// <summary>The type as declared, or 404 before anything else runs (the permission check needs a real type to name).</summary>
    private static string Known(string module, string type) => PackCatalog.Require(module, type).Key;
}
