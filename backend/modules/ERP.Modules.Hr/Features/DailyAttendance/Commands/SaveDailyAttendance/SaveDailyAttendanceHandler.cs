using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class SaveDailyAttendanceHandler(IAttendanceLogRepository logs, DailyAttendanceRecorder recorder) : ICommandHandler<SaveDailyAttendanceCommand, DailyAttendanceDto>
{
    public async Task<DailyAttendanceDto> Handle(SaveDailyAttendanceCommand command, CancellationToken ct)
    {
        var shift = await recorder.ShiftAsync(null, ct);
        var log = await recorder.UpsertAsync(command.Input, shift, ct);
        await logs.SaveChangesAsync(ct);
        return DailyAttendanceDto.From(log);
    }
}
