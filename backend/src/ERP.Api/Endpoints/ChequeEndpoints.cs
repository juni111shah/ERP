using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record DepositBody(Guid BankAccountId, DateOnly Date);
public sealed record ClearBody(DateOnly Date);
public sealed record BounceBody(DateOnly Date, string Reason);
public sealed record FollowUpBody(string Note, DateOnly? Date = null);

/// <summary>The cheque / post-dated cheque register.</summary>
public static class ChequeEndpoints
{
    public static void MapCheques(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/finance/cheques").WithTags("Cheques");
        g.MapPagedList<PaymentInstrument, ChequeSummaryRow>("", CorePermissions.ChequesRead,
            c => new ChequeSummaryRow(c.Id, c.Number, c.Kind, c.Direction, c.InstrumentNo, c.BankName, c.PartyId, c.PartyName, c.Amount, c.InstrumentDate, c.Status, c.CurrencyCode, c.BouncedOn, c.FollowUpOn));
        g.MapGet("/summary", (DateOnly? asOf, ISender s, CancellationToken ct) => s.Send(new GetChequeSummary(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct)).RequirePermission(CorePermissions.ChequesRead);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetCheque(id), ct)).RequirePermission(CorePermissions.ChequesRead);
        g.MapPost("", async (ChequeInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new RegisterCheque(input), ct);
            return Results.Created($"/api/v1/finance/cheques/{created.Id}", created);
        }).RequirePermission(CorePermissions.ChequesWrite);
        g.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelCheque(id), ct)).RequirePermission(CorePermissions.ChequesWrite);
        g.MapPost("/{id:guid}/deposit", (Guid id, DepositBody b, ISender s, CancellationToken ct) => s.Send(new DepositCheque(id, b.BankAccountId, b.Date), ct)).RequirePermission(CorePermissions.ChequesApprove);
        g.MapPost("/{id:guid}/clear", (Guid id, ClearBody b, ISender s, CancellationToken ct) => s.Send(new ClearCheque(id, b.Date), ct)).RequirePermission(CorePermissions.ChequesApprove);
        g.MapPost("/{id:guid}/bounce", (Guid id, BounceBody b, ISender s, CancellationToken ct) => s.Send(new BounceCheque(id, b.Date, b.Reason), ct)).RequirePermission(CorePermissions.ChequesApprove);
        g.MapPost("/{id:guid}/follow-up", (Guid id, FollowUpBody b, ISender s, CancellationToken ct) => s.Send(new LogChequeFollowUp(id, b.Date, b.Note), ct)).RequirePermission(CorePermissions.ChequesWrite);
        g.MapPost("/{id:guid}/replace", (Guid id, ChequeInput replacement, ISender s, CancellationToken ct) => s.Send(new ReplaceCheque(id, replacement), ct)).RequirePermission(CorePermissions.ChequesApprove);
    }
}
