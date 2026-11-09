using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class AddRosterHandler(IShiftRosterRepository roster, IReadRepository<Shift> shifts, IEmployeeRepository employees, ILegalEntityGuard guard) : ICommandHandler<AddRosterCommand, IReadOnlyList<RosterDto>>
{
    public async Task<IReadOnlyList<RosterDto>> Handle(AddRosterCommand command, CancellationToken ct)
    {
        // 1. An active shift and employees that all exist.
        var shift = await shifts.GetAsync(command.ShiftId, ct);
        if (!shift.IsActive) throw new ConflictException("This shift is not active.");
        var i = command.Input; var ids = i.EmployeeIds.Distinct().ToList();
        var staff = await employees.ListByIdsAsync(ids, ct);
        if (staff.Count != ids.Count) throw new NotFoundException("One of the employees was not found.");

        // 2. One roster entry per employee, and save.
        var added = new List<ShiftRoster>();
        foreach (var e in staff)
        {
            await guard.RequireAsync(e.LegalEntityId, ct);
            var r = new ShiftRoster { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, ShiftId = shift.Id, FromDate = i.FromDate, ToDate = i.ToDate };
            roster.Add(r); added.Add(r);
        }
        await roster.SaveChangesAsync(ct);
        return added.Select(RosterDto.From).ToList();
    }
}
