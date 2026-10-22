using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record ReverseBody(DateOnly? Date, string? Memo);
public sealed record LockBooksBody(DateOnly? Through);

/// <summary>Journals, period lock and the ledger reports. Thin: bind, send one MediatR request, shape the result.</summary>
public static class LedgerEndpoints
{
    public static void MapLedger(this RouteGroupBuilder api)
    {
        var journals = api.MapGroup("/finance/journals").WithTags("Finance");
        journals.MapPagedList<JournalEntry, JournalSummaryDto>("", CorePermissions.JournalsRead,
            j => new JournalSummaryDto(j.Id, j.Number, j.LegalEntityId, j.Date, j.Memo, j.SourceType, j.Status, j.Amount, j.CurrencyCode));
        journals.MapGet("/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetJournal(id), ct)).RequirePermission(CorePermissions.JournalsRead);
        journals.MapPost("", async (JournalInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateJournal(input), ct);
            return Results.Created($"/api/v1/finance/journals/{created.Id}", created);
        }).RequirePermission(CorePermissions.JournalsWrite);
        journals.MapPut("/{id:guid}", (Guid id, JournalInput input, ISender sender, CancellationToken ct) => sender.Send(new UpdateJournal(id, input), ct)).RequirePermission(CorePermissions.JournalsWrite);
        journals.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => { await sender.Send(new DeleteJournal(id), ct); return Results.NoContent(); }).RequirePermission(CorePermissions.JournalsWrite);
        journals.MapPost("/{id:guid}/post", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new PostJournal(id), ct)).RequirePermission(CorePermissions.JournalsPost);
        journals.MapPost("/{id:guid}/reverse", (Guid id, ReverseBody? body, ISender sender, CancellationToken ct) => sender.Send(new ReverseJournal(id, body?.Date, body?.Memo), ct)).RequirePermission(CorePermissions.JournalsPost);

        api.MapPut("/legal-entities/{id:guid}/books-lock", async (Guid id, LockBooksBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new LockBooks(id, body.Through), ct);
            return Results.NoContent();
        }).WithTags("Organization").RequirePermission(CorePermissions.EntitiesWrite);

        var reports = api.MapGroup("/finance/reports").WithTags("Finance reports");
        reports.MapGet("/trial-balance", (DateOnly? from, DateOnly to, Guid? legalEntityId, ISender sender, CancellationToken ct) => sender.Send(new GetTrialBalance(from, to, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapGet("/general-ledger", (Guid accountId, DateOnly? from, DateOnly to, Guid? legalEntityId, ISender sender, CancellationToken ct) => sender.Send(new GetGeneralLedger(accountId, from, to, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapGet("/income-statement", (DateOnly from, DateOnly to, Guid? legalEntityId, ISender sender, CancellationToken ct) => sender.Send(new GetIncomeStatement(from, to, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapGet("/balance-sheet", (DateOnly asOf, Guid? legalEntityId, ISender sender, CancellationToken ct) => sender.Send(new GetBalanceSheet(asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
    }
}
