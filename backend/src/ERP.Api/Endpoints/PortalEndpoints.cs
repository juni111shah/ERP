using ERP.Application.Portal;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record PortalCodeRequest(string Workspace, string Email, string? Kind = null);
public sealed record PortalVerifyRequest(string Workspace, string Email, string Code, string? Kind = null);

/// <summary>Customer (tenant) and vendor self-service: email-code sign-in, then read-only access to their own documents.</summary>
public static class PortalEndpoints
{
    public static void MapPortal(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/portal").WithTags("Portal");
        g.MapPost("/auth/request-code", async (PortalCodeRequest r, ISender s, CancellationToken ct) => { await s.Send(new RequestPortalCode(r.Workspace, r.Email, r.Kind), ct); return Results.NoContent(); }).AllowAnonymous().RequireRateLimiting("login");
        g.MapPost("/auth/verify", (PortalVerifyRequest r, ISender s, CancellationToken ct) => s.Send(new VerifyPortalCode(r.Workspace, r.Email, r.Code, r.Kind), ct)).AllowAnonymous().RequireRateLimiting("login");

        g.MapGet("/me", (ISender s, CancellationToken ct) => s.Send(new GetPortalProfile(), ct)).RequireAuthorization("portal");

        var vendor = g.MapGroup("/vendor").RequireAuthorization("portal.vendor");
        vendor.MapGet("/bills", (ISender s, CancellationToken ct) => s.Send(new ListVendorBills(), ct));
        vendor.MapGet("/payments", (ISender s, CancellationToken ct) => s.Send(new ListVendorPayments(), ct));
        vendor.MapGet("/statement", (DateOnly? asOf, ISender s, CancellationToken ct) => s.Send(new GetVendorStatement(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct));

        var secured = g.MapGroup("").RequireAuthorization("portal.customer");
        secured.MapGet("/invoices", (ISender s, CancellationToken ct) => s.Send(new ListPortalInvoices(), ct));
        secured.MapGet("/receipts", (ISender s, CancellationToken ct) => s.Send(new ListPortalReceipts(), ct));
        secured.MapGet("/statement", (DateOnly? asOf, ISender s, CancellationToken ct) => s.Send(new GetPortalStatement(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct));
    }
}
