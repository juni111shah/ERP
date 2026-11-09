using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteShiftHandler(IRepository<Shift> shifts, IReadRepository<Employee> employees) : ICommandHandler<DeleteShiftCommand>
{
    public async Task<Unit> Handle(DeleteShiftCommand command, CancellationToken ct)
    {
        // 1. Only a shift nobody is on can go.
        var s = await shifts.GetForUpdateAsync(command.Id, ct);
        var id = s.Id;
        if (await employees.AnyAsync(e => e.ShiftId == id, ct)) throw new ConflictException("Employees are on this shift. Move them first.");

        // 2. Remove and save.
        shifts.Remove(s);
        await shifts.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
