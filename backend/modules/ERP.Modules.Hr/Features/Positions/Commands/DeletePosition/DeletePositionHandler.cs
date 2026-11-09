using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeletePositionHandler(IRepository<Position> positions, IReadRepository<Employee> employees) : ICommandHandler<DeletePositionCommand>
{
    public async Task<Unit> Handle(DeletePositionCommand command, CancellationToken ct)
    {
        // 1. Only a position nobody holds can go.
        var p = await positions.GetForUpdateAsync(command.Id, ct);
        var id = p.Id;
        if (await employees.AnyAsync(e => e.PositionId == id, ct)) throw new ConflictException("Employees hold this position. Move them first.");

        // 2. Remove and save.
        positions.Remove(p);
        await positions.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
