using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteRosterHandler(IShiftRosterRepository roster) : ICommandHandler<DeleteRosterCommand>
{
    public async Task<Unit> Handle(DeleteRosterCommand command, CancellationToken ct)
    {
        roster.Remove(await roster.GetForUpdateAsync(command.Id, ct));
        await roster.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
