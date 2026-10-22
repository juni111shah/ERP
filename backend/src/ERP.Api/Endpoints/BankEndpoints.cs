using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record ImportBody(Guid LegalEntityId, List<StatementLineInput> Lines);
public sealed record MatchBody(Guid JournalLineId);
public sealed record BookBody(Guid CounterAccountId, string? Memo);
public sealed record MatchAllBody(Guid LegalEntityId);

/// <summary>Bank and cash: balances, statement import, reconciliation and transfers between accounts.</summary>
public static class BankEndpoints
{
    public static void MapBank(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/finance/bank").WithTags("Bank and cash");
        g.MapGet("/position", (DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetCashPosition(asOf, legalEntityId), ct)).RequirePermission(CorePermissions.BankRead);
        g.MapPagedList<BankStatementLine, StatementLineDto>("/statement-lines", CorePermissions.BankRead, l => new StatementLineDto(l.Id, l.AccountId, l.Date, l.Description, l.Reference, l.Amount, l.Status, l.MatchedJournalLineId));
        g.MapGet("/accounts/{accountId:guid}/reconciliation", (Guid accountId, DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetReconciliation(accountId, legalEntityId, asOf), ct)).RequirePermission(CorePermissions.BankRead);
        g.MapPost("/accounts/{accountId:guid}/import", (Guid accountId, ImportBody body, ISender s, CancellationToken ct) => s.Send(new ImportStatement(body.LegalEntityId, accountId, body.Lines), ct)).RequirePermission(CorePermissions.BankWrite);
        g.MapPost("/accounts/{accountId:guid}/auto-match", async (Guid accountId, MatchAllBody body, ISender s, CancellationToken ct) => new { matched = await s.Send(new AutoMatchStatement(body.LegalEntityId, accountId), ct) }).RequirePermission(CorePermissions.BankWrite);
        g.MapPost("/statement-lines/{id:guid}/match", (Guid id, MatchBody b, ISender s, CancellationToken ct) => s.Send(new MatchStatementLine(id, b.JournalLineId), ct)).RequirePermission(CorePermissions.BankWrite);
        g.MapPost("/statement-lines/{id:guid}/unmatch", (Guid id, ISender s, CancellationToken ct) => s.Send(new UnmatchStatementLine(id), ct)).RequirePermission(CorePermissions.BankWrite);
        g.MapPost("/statement-lines/{id:guid}/book", (Guid id, BookBody b, ISender s, CancellationToken ct) => s.Send(new BookStatementLine(id, b.CounterAccountId, b.Memo), ct)).RequirePermission(CorePermissions.BankWrite);
        g.MapPost("/transfers", async (TransferFunds cmd, ISender s, CancellationToken ct) => Results.Created("/api/v1/finance/journals/" + await s.Send(cmd, ct), null)).RequirePermission(CorePermissions.BankWrite);
    }
}
