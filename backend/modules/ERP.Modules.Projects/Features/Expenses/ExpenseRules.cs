using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>Who may change an expense, and how the form fills it in. Shared by the expense handlers.</summary>
public sealed class ExpenseRules(ProjectPeople people, ProjectAccess access, ICurrentUser user)
{
    /// <summary>People change their own expenses; changing someone else's takes approval or project-manager rights.</summary>
    public void EnsureMayChange(ProjectExpense e)
    {
        if (e.WorkerUserId is null || e.WorkerUserId == user.UserId) return;
        if (access.Has(P.ExpensesApprove) || access.Has(P.ProjectsManage)) return;
        throw new ForbiddenException("Only the person who paid or a manager can change this expense.");
    }

    public async Task FillAsync(ProjectExpense e, Project p, ExpenseInput i, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        if (i.Date > today.AddDays(1)) throw new FieldValidationException("date", "An expense cannot be dated in the future.");
        string? name = i.WorkerName?.Trim();
        if (i.WorkerUserId is { } uid) name = await people.RequireNameAsync(uid, "workerUserId", ct);
        e.Date = i.Date; e.Category = i.Category.Trim(); e.Description = i.Description.Trim(); e.Amount = ProjectMath.Round(i.Amount);
        e.IsBillable = i.IsBillable && p.BillingType != ProjectBillingType.NonBillable; e.MarkupPercent = i.MarkupPercent; e.WorkerUserId = i.WorkerUserId; e.WorkerName = name;
    }
}
