using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>The ledger, cash and tax reports.</summary>
[Route("api/v1/finance/reports")]
[RequirePermission(CorePermissions.ReportsRead)]
public sealed class LedgerReportsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("trial-balance")]
    public Task<TrialBalanceDto> TrialBalance(DateOnly? from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetTrialBalanceQuery(from, to, legalEntityId), ct);

    [HttpGet("general-ledger")]
    public Task<GeneralLedgerDto> GeneralLedger(Guid accountId, DateOnly? from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetGeneralLedgerQuery(accountId, from, to, legalEntityId), ct);

    [HttpGet("income-statement")]
    public Task<IncomeStatementDto> IncomeStatement(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetIncomeStatementQuery(from, to, legalEntityId), ct);

    [HttpGet("tax-summary")]
    public Task<TaxSummaryDto> TaxSummary(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetTaxSummaryQuery(from, to, legalEntityId), ct);

    [HttpGet("cash-movements")]
    public Task<CashMovementsDto> CashMovements(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetCashMovementsQuery(from, to, legalEntityId), ct);

    [HttpGet("cash-forecast")]
    public Task<CashForecastDto> CashForecast(int? weeks, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetCashForecastQuery(weeks ?? 13, legalEntityId), ct);

    [HttpGet("tax-return")]
    public Task<TaxReturnDto> TaxReturn(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetTaxReturnQuery(from, to, legalEntityId), ct);

    [HttpGet("balance-sheet")]
    public Task<BalanceSheetDto> BalanceSheet(DateOnly asOf, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetBalanceSheetQuery(asOf, legalEntityId), ct);

    [HttpGet("cash-flow")]
    public Task<CashFlowStatementDto> CashFlow(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetCashFlowStatementQuery(from, to, legalEntityId), ct);
}
