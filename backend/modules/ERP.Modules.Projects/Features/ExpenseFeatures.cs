using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed record ExpenseInput(
    Guid ProjectId,
    DateOnly Date,
    string Category,
    string Description,
    decimal Amount,
    bool IsBillable = true,
    decimal MarkupPercent = 0m,
    Guid? WorkerUserId = null,
    string? WorkerName = null);

public sealed record ExpenseDto(
    Guid Id, Guid ProjectId, string ProjectName, DateOnly Date, string Category, string Description, decimal Amount, bool IsBillable, decimal MarkupPercent, decimal ChargeAmount,
    Guid? WorkerUserId, string? WorkerName, ExpenseStatus Status, string? RejectReason, bool IsBilled, Guid? InvoiceId, bool WrittenOff, DateTime CreatedAt);

public sealed record GetExpense(Guid Id) : IQuery<ExpenseDto>, IRequirePermission { public string RequiredPermission => P.ExpensesRead; }
public sealed record CreateExpense(ExpenseInput Input) : ICommand<ExpenseDto>, IRequirePermission { public string RequiredPermission => P.ExpensesManage; }
public sealed record UpdateExpense(Guid Id, ExpenseInput Input) : ICommand<ExpenseDto>, IRequirePermission { public string RequiredPermission => P.ExpensesManage; }
public sealed record DeleteExpense(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ExpensesManage; }
public sealed record SubmitExpenses(IReadOnlyList<Guid> Ids) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.ExpensesManage; }
public sealed record DecideExpenses(IReadOnlyList<Guid> Ids, bool Approve, string? Reason) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.ExpensesApprove; }

public sealed class ExpenseInputValidator : AbstractValidator<ExpenseInput>
{
    public ExpenseInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0).LessThan(1_000_000_000m);
        RuleFor(x => x.MarkupPercent).InclusiveBetween(0, 500);
        RuleFor(x => x.WorkerName).MaximumLength(100);
    }
}

public sealed class CreateExpenseValidator : AbstractValidator<CreateExpense> { public CreateExpenseValidator() => RuleFor(x => x.Input).SetValidator(new ExpenseInputValidator()); }
public sealed class UpdateExpenseValidator : AbstractValidator<UpdateExpense> { public UpdateExpenseValidator() => RuleFor(x => x.Input).SetValidator(new ExpenseInputValidator()); }
public sealed class SubmitExpensesValidator : AbstractValidator<SubmitExpenses> { public SubmitExpensesValidator() => RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500); }
public sealed class DecideExpensesValidator : AbstractValidator<DecideExpenses>
{
    public DecideExpensesValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500);
        RuleFor(x => x.Reason).NotEmpty().When(x => !x.Approve).WithMessage("Say why the expense is rejected.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

internal sealed class ExpenseHandlers(
    IRepository<ProjectExpense> expenses,
    IReadRepository<Project> projects,
    ProjectPeople people,
    ProjectAccess access,
    ICurrentUser user,
    ISettingsService settings) :
    IRequestHandler<GetExpense, ExpenseDto>,
    IRequestHandler<CreateExpense, ExpenseDto>,
    IRequestHandler<UpdateExpense, ExpenseDto>,
    IRequestHandler<DeleteExpense, Unit>,
    IRequestHandler<SubmitExpenses, int>,
    IRequestHandler<DecideExpenses, int>
{
    public async Task<ExpenseDto> Handle(GetExpense q, CancellationToken ct)
    {
        var e = await expenses.GetAsync(q.Id, ct);
        await access.EnsureProjectAsync(e.ProjectId, ct);
        return await BuildAsync(e, ct);
    }

    /// <summary>People change their own expenses; changing someone else's takes approval or project-manager rights.</summary>
    private void EnsureMayChange(ProjectExpense e)
    {
        if (e.WorkerUserId is null || e.WorkerUserId == user.UserId) return;
        if (access.Has(P.ExpensesApprove) || access.Has(P.ProjectsManage)) return;
        throw new ForbiddenException("Only the person who paid or a manager can change this expense.");
    }

    public async Task<ExpenseDto> Handle(CreateExpense cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureTrackable(p);
        await access.EnsureAsync(p, ct);
        var approval = await settings.GetAsync<bool?>("projects.expenseApproval", p.LegalEntityId, ct) == true;
        var e = new ProjectExpense { ProjectId = p.Id, Status = approval ? ExpenseStatus.Draft : ExpenseStatus.Approved };
        await FillAsync(e, p, i, ct);
        expenses.Add(e);
        await expenses.SaveChangesAsync(ct);
        return await BuildAsync(e, ct);
    }

    public async Task<ExpenseDto> Handle(UpdateExpense cmd, CancellationToken ct)
    {
        var e = await expenses.GetForUpdateAsync(cmd.Id, ct);
        if (e.IsBilled) throw new ConflictException("A billed expense cannot be edited.");
        if (e.WrittenOff) throw new ConflictException("An expense that was written off cannot be edited.");
        if (e.CostJournalId != null) throw new ConflictException("This expense is already in the ledger, so it cannot be changed. Write it off or record a correcting expense.");
        var p = await projects.GetAsync(e.ProjectId, ct);
        ProjectRules.EnsureTrackable(p);
        await access.EnsureAsync(p, ct);
        EnsureMayChange(e);
        if (e.Status is ExpenseStatus.Submitted or ExpenseStatus.Approved && await settings.GetAsync<bool?>("projects.expenseApproval", p.LegalEntityId, ct) == true)
            throw new ConflictException("A submitted or approved expense cannot be edited. Ask for it to be rejected first.");
        await FillAsync(e, p, cmd.Input, ct);
        if (e.Status == ExpenseStatus.Rejected) { e.Status = ExpenseStatus.Draft; e.RejectReason = null; }
        await expenses.SaveChangesAsync(ct);
        return await BuildAsync(e, ct);
    }

    public async Task<Unit> Handle(DeleteExpense cmd, CancellationToken ct)
    {
        var e = await expenses.GetForUpdateAsync(cmd.Id, ct);
        if (e.IsBilled) throw new ConflictException("A billed expense cannot be deleted.");
        if (e.CostJournalId != null) throw new ConflictException("This expense is already in the ledger, so it cannot be deleted. Write it off instead.");
        var p = await projects.GetAsync(e.ProjectId, ct);
        ProjectRules.EnsureTrackable(p);
        await access.EnsureAsync(p, ct);
        EnsureMayChange(e);
        if (e.Status == ExpenseStatus.Submitted) throw new ConflictException("A submitted expense cannot be deleted. Ask for it to be rejected first.");
        if (e.Status == ExpenseStatus.Approved && await settings.GetAsync<bool?>("projects.expenseApproval", p.LegalEntityId, ct) == true)
            throw new ConflictException("An approved expense cannot be deleted. Ask for it to be rejected first.");
        expenses.Remove(e);
        await expenses.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<int> Handle(SubmitExpenses cmd, CancellationToken ct)
    {
        var rows = await expenses.Tracked(new Specification<ProjectExpense>(e => cmd.Ids.Contains(e.Id))).ToListAsync(ct);
        if (rows.Count != cmd.Ids.Distinct().Count()) throw new NotFoundException("One of the expenses was not found.");
        await access.EnsureProjectsAsync(rows.Select(e => e.ProjectId), ct);
        foreach (var e in rows) EnsureMayChange(e);
        var n = 0;
        foreach (var e in rows.Where(e => e.Status is ExpenseStatus.Draft or ExpenseStatus.Rejected)) { e.Status = ExpenseStatus.Submitted; e.RejectReason = null; n++; }
        await expenses.SaveChangesAsync(ct);
        return n;
    }

    public async Task<int> Handle(DecideExpenses cmd, CancellationToken ct)
    {
        var rows = await expenses.Tracked(new Specification<ProjectExpense>(e => cmd.Ids.Contains(e.Id))).ToListAsync(ct);
        if (rows.Count != cmd.Ids.Distinct().Count()) throw new NotFoundException("One of the expenses was not found.");
        await access.EnsureProjectsAsync(rows.Select(e => e.ProjectId), ct);
        if (cmd.Approve && user.UserId is { } me && rows.Any(e => e.WorkerUserId == me && e.Status == ExpenseStatus.Submitted)
            && await settings.GetAsync<bool?>("projects.allowSelfApproval", null, ct) != true)
            throw new ConflictException("You cannot approve your own expense. Ask another approver.");
        var n = 0;
        foreach (var e in rows.Where(e => e.Status == ExpenseStatus.Submitted))
        {
            e.Status = cmd.Approve ? ExpenseStatus.Approved : ExpenseStatus.Rejected;
            e.RejectReason = cmd.Approve ? null : cmd.Reason?.Trim();
            n++;
        }
        await expenses.SaveChangesAsync(ct);
        return n;
    }

    private async Task FillAsync(ProjectExpense e, Project p, ExpenseInput i, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (i.Date > today.AddDays(1)) throw new FieldValidationException("date", "An expense cannot be dated in the future.");
        string? name = i.WorkerName?.Trim();
        if (i.WorkerUserId is { } uid) name = await people.RequireNameAsync(uid, "workerUserId", ct);
        e.Date = i.Date; e.Category = i.Category.Trim(); e.Description = i.Description.Trim(); e.Amount = ProjectMath.Round(i.Amount);
        e.IsBillable = i.IsBillable && p.BillingType != ProjectBillingType.NonBillable; e.MarkupPercent = i.MarkupPercent; e.WorkerUserId = i.WorkerUserId; e.WorkerName = name;
    }

    private async Task<ExpenseDto> BuildAsync(ProjectExpense e, CancellationToken ct)
    {
        var name = (await projects.GetAsync(e.ProjectId, ct)).Name;
        return new ExpenseDto(e.Id, e.ProjectId, name, e.Date, e.Category, e.Description, e.Amount, e.IsBillable, e.MarkupPercent, BillingLines.Charge(e), e.WorkerUserId, e.WorkerName, e.Status, e.RejectReason, e.IsBilled, e.InvoiceId, e.WrittenOff, e.CreatedAt);
    }
}
