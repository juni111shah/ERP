using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Finance;

internal sealed class DeleteBudgetHandler(IBudgetRepository budgets) : ICommandHandler<DeleteBudgetCommand>
{
    public async Task<Unit> Handle(DeleteBudgetCommand cmd, CancellationToken ct)
    {
        budgets.Remove(await budgets.GetForUpdateAsync(cmd.Id, ct));
        await budgets.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
