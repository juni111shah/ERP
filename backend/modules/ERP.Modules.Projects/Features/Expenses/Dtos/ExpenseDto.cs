using System.Linq.Expressions;
using ERP.Modules.Projects.Domain;

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
    Guid? WorkerUserId, string? WorkerName, ExpenseStatus Status, string? RejectReason, bool IsBilled, Guid? InvoiceId, bool WrittenOff, DateTime CreatedAt)
{
    /// <summary>The list projection (translated to SQL); the project name is filled in afterwards by <see cref="ProjectListEnrich.Expenses"/>.</summary>
    public static readonly Expression<Func<ProjectExpense, ExpenseDto>> Projection = e => new ExpenseDto(
        e.Id, e.ProjectId, "", e.Date, e.Category, e.Description, e.Amount, e.IsBillable, e.MarkupPercent, e.Amount * (1 + e.MarkupPercent / 100m), e.WorkerUserId, e.WorkerName, e.Status, e.RejectReason, e.IsBilled, e.InvoiceId, e.WrittenOff, e.CreatedAt);
}
