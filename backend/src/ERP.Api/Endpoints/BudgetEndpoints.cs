using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

/// <summary>Budgets and the budget-vs-actual report.</summary>
public static class BudgetEndpoints
{
    public static void MapBudgets(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/finance/budgets").WithTags("Budgets");
        g.MapPagedList<Budget, BudgetSummaryDto>("", CorePermissions.BudgetsRead, b => new BudgetSummaryDto(b.Id, b.LegalEntityId, b.Name, b.FiscalYear, b.IsActive));
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetBudget(id), ct)).RequirePermission(CorePermissions.BudgetsRead);
        g.MapPost("", async (BudgetInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateBudget(input), ct);
            return Results.Created($"/api/v1/finance/budgets/{created.Id}", created);
        }).RequirePermission(CorePermissions.BudgetsWrite);
        g.MapPut("/{id:guid}", (Guid id, BudgetInput input, ISender s, CancellationToken ct) => s.Send(new UpdateBudget(id, input), ct)).RequirePermission(CorePermissions.BudgetsWrite);
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteBudget(id), ct); return Results.NoContent(); }).RequirePermission(CorePermissions.BudgetsWrite);
        g.MapGet("/{id:guid}/vs-actual", (Guid id, int? fromMonth, int? toMonth, ISender s, CancellationToken ct) => s.Send(new GetBudgetVsActual(id, fromMonth ?? 1, toMonth ?? 12), ct)).RequirePermission(CorePermissions.BudgetsRead);
    }
}
