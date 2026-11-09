using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Bank and cash: balances, statement import, reconciliation and transfers between accounts.</summary>
[CapabilityGate("banking")]
[Route("api/v1/finance/bank")]
public sealed class BankController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("position")]
    [RequirePermission(CorePermissions.BankRead)]
    public Task<CashPositionDto> Position(DateOnly asOf, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetCashPositionQuery(asOf, legalEntityId), ct);

    [HttpGet("statement-lines")]
    [RequirePermission(CorePermissions.BankRead)]
    public Task<PagedResult<StatementLineDto>> StatementLines([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, StatementLineDto.Projection, CorePermissions.BankRead, ct);

    [HttpGet("accounts/{accountId:guid}/reconciliation")]
    [RequirePermission(CorePermissions.BankRead)]
    public Task<ReconciliationDto> Reconciliation(Guid accountId, DateOnly asOf, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetReconciliationQuery(accountId, legalEntityId, asOf), ct);

    [HttpPost("accounts/{accountId:guid}/import")]
    [RequirePermission(CorePermissions.BankWrite)]
    public Task<ImportResult> Import(Guid accountId, ImportRequest body, CancellationToken ct)
        => Mediator.Send(new ImportStatementCommand(body.LegalEntityId, accountId, body.Lines), ct);

    [HttpPost("accounts/{accountId:guid}/auto-match")]
    [RequirePermission(CorePermissions.BankWrite)]
    public async Task<object> AutoMatch(Guid accountId, MatchAllRequest body, CancellationToken ct)
        => new { matched = await Mediator.Send(new AutoMatchStatementCommand(body.LegalEntityId, accountId), ct) };

    [HttpPost("statement-lines/{id:guid}/match")]
    [RequirePermission(CorePermissions.BankWrite)]
    public Task<StatementLineDto> Match(Guid id, MatchRequest body, CancellationToken ct)
        => Mediator.Send(new MatchStatementLineCommand(id, body.JournalLineId), ct);

    [HttpPost("statement-lines/{id:guid}/unmatch")]
    [RequirePermission(CorePermissions.BankWrite)]
    public Task<StatementLineDto> Unmatch(Guid id, CancellationToken ct)
        => Mediator.Send(new UnmatchStatementLineCommand(id), ct);

    [HttpPost("statement-lines/{id:guid}/book")]
    [RequirePermission(CorePermissions.BankWrite)]
    public Task<StatementLineDto> Book(Guid id, BookRequest body, CancellationToken ct)
        => Mediator.Send(new BookStatementLineCommand(id, body.CounterAccountId, body.Memo), ct);

    [HttpPost("transfers")]
    [RequirePermission(CorePermissions.BankWrite)]
    public async Task<IActionResult> Transfer(TransferFundsCommand command, CancellationToken ct)
        => Created("/api/v1/finance/journals/" + await Mediator.Send(command, ct), null);

    /// <summary>The legal entity and the statement lines to import.</summary>
    public sealed record ImportRequest(Guid LegalEntityId, List<StatementLineInput> Lines);

    /// <summary>The ledger line to match the statement line to.</summary>
    public sealed record MatchRequest(Guid JournalLineId);

    /// <summary>The account to book the statement line against, and an optional memo (the line's text by default).</summary>
    public sealed record BookRequest(Guid CounterAccountId, string? Memo);

    /// <summary>The legal entity whose unmatched lines are matched.</summary>
    public sealed record MatchAllRequest(Guid LegalEntityId);
}
