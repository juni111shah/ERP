using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SaveBudgetLineHandler(IProjectBudgetLineRepository budgetLines, ProjectAccess access) : ICommandHandler<SaveBudgetLineCommand, BudgetLineDto>
{
    public async Task<BudgetLineDto> Handle(SaveBudgetLineCommand command, CancellationToken ct)
    {
        // 1. One line per category of a project.
        var i = command.Input;
        await access.EnsureProjectAsync(i.ProjectId, ct);
        var category = i.Category.Trim();
        var pid = i.ProjectId;
        var existing = await budgetLines.ListOfProjectAsync(pid, ct);
        if (existing.Any(x => x.Id != command.Id && string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))) throw new ConflictException($"There is already a budget line for {category}.");

        // 2. Create or load it, fill it in and save.
        ProjectBudgetLine line;
        if (command.Id is { } id) line = await budgetLines.GetForUpdateAsync(id, ct); else { line = new ProjectBudgetLine { ProjectId = pid }; budgetLines.Add(line); }
        line.Category = category; line.Amount = i.Amount; line.Hours = i.Hours;
        await budgetLines.SaveChangesAsync(ct);
        return new BudgetLineDto(line.Id, line.Category, line.Amount, line.Hours, 0m, 0m);
    }
}
