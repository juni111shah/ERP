using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>Builds the expense view with its project name and the amount charged. Shared by the expense handlers.</summary>
public sealed class ExpenseDtoBuilder(IProjectRepository projects)
{
    public async Task<ExpenseDto> BuildAsync(ProjectExpense e, CancellationToken ct)
    {
        var name = (await projects.GetAsync(e.ProjectId, ct)).Name;
        return new ExpenseDto(e.Id, e.ProjectId, name, e.Date, e.Category, e.Description, e.Amount, e.IsBillable, e.MarkupPercent, BillingLines.Charge(e), e.WorkerUserId, e.WorkerName, e.Status, e.RejectReason, e.IsBilled, e.InvoiceId, e.WrittenOff, e.CreatedAt);
    }
}
