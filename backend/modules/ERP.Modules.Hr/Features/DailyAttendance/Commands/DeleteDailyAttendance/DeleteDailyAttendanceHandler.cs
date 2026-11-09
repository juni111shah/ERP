using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteDailyAttendanceHandler(IAttendanceLogRepository logs) : ICommandHandler<DeleteDailyAttendanceCommand>
{
    public async Task<Unit> Handle(DeleteDailyAttendanceCommand command, CancellationToken ct)
    {
        logs.Remove(await logs.GetForUpdateAsync(command.Id, ct));
        await logs.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
