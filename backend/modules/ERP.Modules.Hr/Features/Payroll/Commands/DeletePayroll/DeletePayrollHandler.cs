using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeletePayrollHandler(IPayrollRunRepository runs, PayrollRunRules rules) : ICommandHandler<DeletePayrollCommand>
{
    public async Task<Unit> Handle(DeletePayrollCommand command, CancellationToken ct)
    {
        // 1. Load the draft and withdraw a pending approval.
        var run = await rules.LoadDraftAsync(command.Id, ct);
        await rules.WithdrawPendingAsync(run, ct);

        // 2. Remove it with its loan recoveries and statutory rows, and save.
        await rules.RemoveRecoveriesAsync(run.Id, ct);
        await rules.RemoveStatutoryAsync(run.Id, ct);
        runs.Remove(run);
        await runs.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
