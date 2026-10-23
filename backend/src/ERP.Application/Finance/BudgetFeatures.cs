using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record BudgetLineInput(Guid AccountId, int Month, decimal Amount);
public sealed record BudgetInput(Guid LegalEntityId, string Name, int FiscalYear, string? Notes, bool IsActive, IReadOnlyList<BudgetLineInput> Lines);
public sealed record BudgetLineDto(Guid AccountId, string AccountCode, string AccountName, int Month, decimal Amount);
public sealed record BudgetDto(Guid Id, Guid LegalEntityId, string Name, int FiscalYear, string? Notes, bool IsActive, decimal TotalIncome, decimal TotalExpense, IReadOnlyList<BudgetLineDto> Lines);
public sealed record BudgetSummaryDto(Guid Id, Guid LegalEntityId, string Name, int FiscalYear, bool IsActive);

public sealed record BudgetVsActualRow(Guid AccountId, string Code, string Name, AccountType Type, decimal Budget, decimal Actual, decimal Variance, decimal? VariancePercent);
public sealed record BudgetVsActualDto(Guid BudgetId, string Name, int FiscalYear, int FromMonth, int ToMonth, IReadOnlyList<BudgetVsActualRow> Rows,
    decimal BudgetIncome, decimal ActualIncome, decimal BudgetExpense, decimal ActualExpense, decimal BudgetNet, decimal ActualNet);

public sealed record GetBudget(Guid Id) : IQuery<BudgetDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BudgetsRead; }
public sealed record CreateBudget(BudgetInput Input) : ICommand<BudgetDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BudgetsWrite; }
public sealed record UpdateBudget(Guid Id, BudgetInput Input) : ICommand<BudgetDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BudgetsWrite; }
public sealed record DeleteBudget(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => CorePermissions.BudgetsWrite; }
/// <summary>Budget against the posted ledger for a range of months. Income variance is actual − budget; expense variance is budget − actual (positive = favourable).</summary>
public sealed record GetBudgetVsActual(Guid BudgetId, int FromMonth, int ToMonth) : IQuery<BudgetVsActualDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BudgetsRead; }

public sealed class BudgetInputValidator : AbstractValidator<BudgetInput>
{
    public BudgetInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Lines).NotNull().Must(l => l is null || l.Count <= 2000).WithMessage("A budget can have at most 2000 lines.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.AccountId).NotEmpty();
            l.RuleFor(x => x.Month).InclusiveBetween(1, 12);
            l.RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => (x.AccountId, x.Month)).Distinct().Count() == l.Count).WithMessage("Each account can appear once per month.");
    }
}
public sealed class CreateBudgetValidator : AbstractValidator<CreateBudget> { public CreateBudgetValidator() => RuleFor(x => x.Input).SetValidator(new BudgetInputValidator()); }
public sealed class UpdateBudgetValidator : AbstractValidator<UpdateBudget> { public UpdateBudgetValidator() => RuleFor(x => x.Input).SetValidator(new BudgetInputValidator()); }
public sealed class GetBudgetVsActualValidator : AbstractValidator<GetBudgetVsActual>
{
    public GetBudgetVsActualValidator() { RuleFor(x => x.FromMonth).InclusiveBetween(1, 12); RuleFor(x => x.ToMonth).InclusiveBetween(1, 12).GreaterThanOrEqualTo(x => x.FromMonth); }
}

internal sealed class BudgetHandlers(IRepository<Budget> budgets, IRepository<BudgetLine> lineRows, IReadRepository<Account> accounts, IReadRepository<JournalEntry> journals, ILegalEntityGuard guard) :
    IRequestHandler<GetBudget, BudgetDto>,
    IRequestHandler<CreateBudget, BudgetDto>,
    IRequestHandler<UpdateBudget, BudgetDto>,
    IRequestHandler<DeleteBudget, Unit>,
    IRequestHandler<GetBudgetVsActual, BudgetVsActualDto>
{
    public async Task<BudgetDto> Handle(GetBudget q, CancellationToken ct) => await ToDtoAsync(await LoadAsync(q.Id, ct), ct);

    public async Task<BudgetDto> Handle(CreateBudget cmd, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(cmd.Input.LegalEntityId, ct);
        if (await budgets.AnyAsync(b => b.LegalEntityId == entity.Id && b.FiscalYear == cmd.Input.FiscalYear && b.Name == cmd.Input.Name.Trim(), ct))
            throw new ConflictException("A budget with this name already exists for that year.");
        var budget = new Budget { LegalEntityId = entity.Id };
        Map(cmd.Input, budget);
        budget.Lines.AddRange(await LinesAsync(budget, cmd.Input, ct));
        budgets.Add(budget);
        await budgets.SaveChangesAsync(ct);
        return await ToDtoAsync(budget, ct);
    }

    public async Task<BudgetDto> Handle(UpdateBudget cmd, CancellationToken ct)
    {
        var budget = await LoadAsync(cmd.Id, ct, tracked: true);
        if (cmd.Input.LegalEntityId != budget.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of a budget cannot change.");
        Map(cmd.Input, budget);
        foreach (var old in budget.Lines.ToList()) lineRows.Remove(old);
        var fresh = await LinesAsync(budget, cmd.Input, ct);
        lineRows.AddRange(fresh);
        await budgets.SaveChangesAsync(ct);
        budget.Lines.Clear();
        budget.Lines.AddRange(fresh);
        return await ToDtoAsync(budget, ct);
    }

    public async Task<Unit> Handle(DeleteBudget cmd, CancellationToken ct)
    {
        budgets.Remove(await budgets.GetForUpdateAsync(cmd.Id, ct));
        await budgets.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<BudgetVsActualDto> Handle(GetBudgetVsActual q, CancellationToken ct)
    {
        var budget = await LoadAsync(q.BudgetId, ct);
        var from = new DateOnly(budget.FiscalYear, q.FromMonth, 1);
        var to = new DateOnly(budget.FiscalYear, q.ToMonth, 1).AddMonths(1).AddDays(-1);
        var movements = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.LegalEntityId == budget.LegalEntityId && j.Date >= from && j.Date <= to))
            .SelectMany(j => j.Lines, (j, l) => new { l.AccountId, l.Debit, l.Credit }).ToListAsync(ct);
        var budgeted = budget.Lines.Where(l => l.Month >= q.FromMonth && l.Month <= q.ToMonth).GroupBy(l => l.AccountId).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
        var actualByAccount = movements.GroupBy(m => m.AccountId).ToDictionary(g => g.Key, g => g.Sum(m => m.Debit - m.Credit));
        var ids = budgeted.Keys.Union(actualByAccount.Keys).ToList();
        var accountRows = await accounts.Query(new Specification<Account>(a => ids.Contains(a.Id) && (a.Type == AccountType.Income || a.Type == AccountType.Expense))).OrderBy(a => a.Code).Select(a => new { a.Id, a.Code, a.Name, a.Type }).ToListAsync(ct);
        var rows = accountRows.Select(a =>
        {
            var plan = budgeted.GetValueOrDefault(a.Id);
            var net = actualByAccount.GetValueOrDefault(a.Id); // debit − credit
            var actual = a.Type == AccountType.Income ? -net : net;
            var variance = a.Type == AccountType.Income ? actual - plan : plan - actual;
            return new BudgetVsActualRow(a.Id, a.Code, a.Name, a.Type, plan, actual, variance, plan == 0 ? null : Math.Round(variance / plan * 100, 1));
        }).Where(r => r.Budget != 0 || r.Actual != 0).ToList();
        decimal Sum(AccountType t, Func<BudgetVsActualRow, decimal> f) => rows.Where(r => r.Type == t).Sum(f);
        var bi = Sum(AccountType.Income, r => r.Budget); var ai = Sum(AccountType.Income, r => r.Actual);
        var be = Sum(AccountType.Expense, r => r.Budget); var ae = Sum(AccountType.Expense, r => r.Actual);
        return new(budget.Id, budget.Name, budget.FiscalYear, q.FromMonth, q.ToMonth, rows, bi, ai, be, ae, bi - be, ai - ae);
    }

    private async Task<Budget> LoadAsync(Guid id, CancellationToken ct, bool tracked = false)
    {
        var spec = new Specification<Budget>(b => b.Id == id).Include(b => b.Lines);
        var query = tracked ? budgets.Tracked(spec) : budgets.Query(spec);
        return await query.FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
    }

    private static void Map(BudgetInput i, Budget b) { b.Name = i.Name.Trim(); b.FiscalYear = i.FiscalYear; b.Notes = i.Notes; b.IsActive = i.IsActive; }

    private async Task<List<BudgetLine>> LinesAsync(Budget budget, BudgetInput input, CancellationToken ct)
    {
        var ids = input.Lines.Select(l => l.AccountId).Distinct().ToList();
        var found = await accounts.Query(new Specification<Account>(a => ids.Contains(a.Id))).Select(a => new { a.Id, a.Type }).ToDictionaryAsync(a => a.Id, a => a.Type, ct);
        foreach (var l in input.Lines)
            if (!found.TryGetValue(l.AccountId, out var type) || type is not (AccountType.Income or AccountType.Expense))
                throw new FieldValidationException("lines", "Budget lines must use income or expense accounts.");
        return input.Lines.Where(l => l.Amount != 0).Select(l => new BudgetLine { BudgetId = budget.Id, AccountId = l.AccountId, Month = l.Month, Amount = l.Amount }).ToList();
    }

    private async Task<BudgetDto> ToDtoAsync(Budget b, CancellationToken ct)
    {
        var ids = b.Lines.Select(l => l.AccountId).Distinct().ToList();
        var map = await accounts.Query(new Specification<Account>(a => ids.Contains(a.Id))).Select(a => new { a.Id, a.Code, a.Name, a.Type }).ToDictionaryAsync(a => a.Id, ct);
        var lines = b.Lines.OrderBy(l => map[l.AccountId].Code).ThenBy(l => l.Month).Select(l => new BudgetLineDto(l.AccountId, map[l.AccountId].Code, map[l.AccountId].Name, l.Month, l.Amount)).ToList();
        return new(b.Id, b.LegalEntityId, b.Name, b.FiscalYear, b.Notes, b.IsActive,
            b.Lines.Where(l => map[l.AccountId].Type == AccountType.Income).Sum(l => l.Amount), b.Lines.Where(l => map[l.AccountId].Type == AccountType.Expense).Sum(l => l.Amount), lines);
    }
}
