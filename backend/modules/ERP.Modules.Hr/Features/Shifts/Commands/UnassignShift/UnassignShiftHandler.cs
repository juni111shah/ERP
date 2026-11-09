using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

internal sealed class UnassignShiftHandler(ShiftAssignments assignments) : ICommandHandler<UnassignShiftCommand, int>
{
    public Task<int> Handle(UnassignShiftCommand command, CancellationToken ct) => assignments.SetAsync(command.EmployeeIds, null, ct);
}
