using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteAttendanceHandler(IAttendanceRecordRepository records, AttendancePeriodGuard period) : ICommandHandler<DeleteAttendanceCommand>
{
    public async Task<Unit> Handle(DeleteAttendanceCommand command, CancellationToken ct)
    {
        var record = await records.GetForUpdateAsync(command.RecordId, ct);
        await period.EnsureNotPostedAsync(record.LegalEntityId, record.Year, record.Month, ct);
        records.Remove(record);
        await records.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
