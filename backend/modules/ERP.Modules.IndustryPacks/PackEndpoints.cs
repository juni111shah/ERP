using ERP.Application.Common;
using ERP.Modularity;
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

/// <summary>The same routes for every pack, under /api/v1/m/{key}. A permission is declared per record type, so each route carries its own.</summary>
internal static class PackEndpoints
{
    public static void Map(RouteGroupBuilder group, PackModule pack)
    {
        var key = pack.Key;
        group.MapGet("/definitions", (ISender s, CancellationToken ct) => s.Send(new GetPackView(key), ct)).RequireAuthorization();
        group.MapGet("/summary", (Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetRecordSummary(key, legalEntityId), ct)).RequireAuthorization();

        foreach (var type in pack.Types)
        {
            var t = type.Key;
            var read = PackModule.ReadPermission(key, t);
            var manage = PackModule.ManagePermission(key, t);
            var g = group.MapGroup("/records/" + t);
            g.MapGet("", ([AsParameters] PagedQuery q, ISender s, CancellationToken ct) => s.Send(new ListRecords(key, t, q), ct)).RequirePermission(read);
            g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetRecord(key, t, id), ct)).RequirePermission(read);
            g.MapPost("", async (RecordInput input, ISender s, CancellationToken ct) =>
            {
                var created = await s.Send(new CreateRecord(key, t, input), ct);
                return Results.Created($"/api/v1/m/{key}/records/{t}/{created.Id}", created);
            }).RequirePermission(manage);
            g.MapPut("/{id:guid}", (Guid id, RecordInput input, ISender s, CancellationToken ct) => s.Send(new UpdateRecord(key, t, id, input), ct)).RequirePermission(manage);
            g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            {
                await s.Send(new DeleteRecord(key, t, id), ct);
                return Results.NoContent();
            }).RequirePermission(manage);
            g.MapPost("/{id:guid}/transition", (Guid id, RecordTransitionBody body, ISender s, CancellationToken ct) => s.Send(new TransitionRecord(key, t, id, body.To, body.Reason), ct)).RequirePermission(manage);
            g.MapPost("/{id:guid}/invoice", (Guid id, RecordBillingBody body, ISender s, CancellationToken ct) => s.Send(new InvoiceRecord(key, t, id, body.Date, body.TaxCodeId, body.LineIds), ct)).RequirePermission(manage);
            g.MapPost("/{id:guid}/bill", (Guid id, RecordBillingBody body, ISender s, CancellationToken ct) => s.Send(new BillRecord(key, t, id, body.Date, body.TaxCodeId, body.VendorReference), ct)).RequirePermission(manage);
            g.MapPost("/{id:guid}/consume", (Guid id, RecordConsumeBody body, ISender s, CancellationToken ct) => s.Send(new ConsumeRecord(key, t, id, body.Date, body.WarehouseId), ct)).RequirePermission(manage);
            g.MapPost("/{id:guid}/progress", (Guid id, RecordProgressBody body, ISender s, CancellationToken ct) => s.Send(new UpdateRecordProgress(key, t, id, body.Lines), ct)).RequirePermission(manage);
        }
    }
}
