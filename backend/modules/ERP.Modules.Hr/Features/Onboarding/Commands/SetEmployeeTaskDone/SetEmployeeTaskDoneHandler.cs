using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class SetEmployeeTaskDoneHandler(IRepository<EmployeeTask> tasks, IClock clock, ICurrentUser user) : ICommandHandler<SetEmployeeTaskDoneCommand, EmployeeTaskDto>
{
    public async Task<EmployeeTaskDto> Handle(SetEmployeeTaskDoneCommand command, CancellationToken ct)
    {
        var task = await tasks.GetForUpdateAsync(command.TaskId, ct);
        task.IsDone = command.Done; task.DoneAt = command.Done ? clock.UtcNow : null; task.DoneByUserId = command.Done ? user.UserId : null;
        await tasks.SaveChangesAsync(ct);
        return EmployeeTaskDto.From(task, DateOnly.FromDateTime(clock.UtcNow));
    }
}
