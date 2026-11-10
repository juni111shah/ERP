using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class BulkSaveDailyAttendanceHandler(IAttendanceLogRepository logs, DailyAttendanceRecorder recorder, IUnitOfWork uow) : ICommandHandler<BulkSaveDailyAttendanceCommand, BulkDailyAttendanceResult>
{
    public Task<BulkDailyAttendanceResult> Handle(BulkSaveDailyAttendanceCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var shift = await recorder.ShiftAsync(null, token);
        var saved = 0; var rejected = new List<string>();
        foreach (var item in command.Items)
        {
            try { await recorder.UpsertAsync(item with { Source = item.Source ?? "import" }, shift, token); await logs.SaveChangesAsync(token); saved++; }
            catch (Exception e) when (e is NotFoundException or ConflictException or FieldValidationException) { rejected.Add($"{item.Date:yyyy-MM-dd}: {e.Message}"); }
        }
        return new BulkDailyAttendanceResult(saved, rejected);
    }, ct);
}
