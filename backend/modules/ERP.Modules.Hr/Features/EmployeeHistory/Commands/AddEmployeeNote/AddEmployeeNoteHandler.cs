using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class AddEmployeeNoteHandler(IEmployeeHistoryEntryRepository entries, IReadRepository<Employee> employees, EmployeeHistoryRecorder recorder, ICurrentUser user)
    : ICommandHandler<AddEmployeeNoteCommand, EmployeeHistoryDto>
{
    public async Task<EmployeeHistoryDto> Handle(AddEmployeeNoteCommand command, CancellationToken ct)
    {
        // 1. Record the note and save.
        var e = await employees.FindAsync(command.EmployeeId, ct) ?? throw new NotFoundException();
        recorder.Add(e, HistoryKind.Note, command.Input.Summary.Trim(), null, null, false, command.Input.EffectiveDate);
        await entries.SaveChangesAsync(ct);

        // 2. Answer with the note as stored.
        var last = await entries.GetLatestNoteAsync(e.Id, ct);
        return EmployeeHistoryDto.From(last, user.HasPermission(P.SalaryRead));
    }
}
