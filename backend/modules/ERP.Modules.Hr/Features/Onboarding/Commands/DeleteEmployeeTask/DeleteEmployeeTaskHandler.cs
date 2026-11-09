using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteEmployeeTaskHandler(IRepository<EmployeeTask> tasks) : ICommandHandler<DeleteEmployeeTaskCommand>
{
    public async Task<Unit> Handle(DeleteEmployeeTaskCommand command, CancellationToken ct)
    {
        tasks.Remove(await tasks.GetForUpdateAsync(command.TaskId, ct));
        await tasks.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
