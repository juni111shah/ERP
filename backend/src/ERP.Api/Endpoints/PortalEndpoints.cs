using ERP.Application.Portal;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record PortalCodeRequest(string Workspace, string Email);
public sealed record PortalVerifyRequest(string Workspace, string Email, string Code);

/// <summary>Customer (tenant) self-service: email-code sign-in, then read-only access to their own invoices, receipts and statement.</summary>
public static class PortalEndpoints
{
    public static void MapPortal(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/portal").WithTags("Portal");
        g.MapPost("/auth/request-code", async (PortalCodeRequest r, ISender s, CancellationToken ct) => { await s.Send(new RequestPortalCode(r.Workspace, r.Email), ct); return Results.NoContent(); }).AllowAnonymous().RequireRateLimiting("login");
        g.MapPost("/auth/verify", (PortalVerifyRequest r, ISender s, CancellationToken ct) => s.Send(new VerifyPortalCode(r.Workspace, r.Email, r.Code), ct)).AllowAnonymous().RequireRateLimiting("login");

        var secured = g.MapGroup("").RequireAuthorization("portal");
        secured.MapGet("/me", (ISender s, CancellationToken ct) => s.Send(new GetPortalProfile(), ct));
        secured.MapGet("/invoices", (ISender s, CancellationToken ct) => s.Send(new ListPortalInvoices(), ct));
        secured.MapGet("/receipts", (ISender s, CancellationToken ct) => s.Send(new ListPortalReceipts(), ct));
        secured.MapGet("/statement", (DateOnly? asOf, ISender s, CancellationToken ct) => s.Send(new GetPortalStatement(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct));
    }
}
