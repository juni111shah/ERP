using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class AssignShiftHandler(IReadRepository<Shift> shifts, ShiftAssignments assignments) : ICommandHandler<AssignShiftCommand, int>
{
    public async Task<int> Handle(AssignShiftCommand command, CancellationToken ct)
    {
        var s = await shifts.GetAsync(command.Id, ct);
        if (!s.IsActive) throw new ConflictException("This shift is not active.");
        return await assignments.SetAsync(command.EmployeeIds, s.Id, ct);
    }
}
