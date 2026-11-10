using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class AddEmployeeTaskHandler(IRepository<EmployeeTask> tasks, IReadRepository<Employee> employees, IClock clock) : ICommandHandler<AddEmployeeTaskCommand, EmployeeTaskDto>
{
    public async Task<EmployeeTaskDto> Handle(AddEmployeeTaskCommand command, CancellationToken ct)
    {
        var e = await employees.FindAsync(command.EmployeeId, ct) ?? throw new NotFoundException();
        var task = new EmployeeTask { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, Kind = command.Input.Kind, Title = command.Input.Title.Trim(), DueDate = command.Input.DueDate, Notes = command.Input.Notes?.Trim() };
        tasks.Add(task);
        await tasks.SaveChangesAsync(ct);
        return EmployeeTaskDto.From(task, DateOnly.FromDateTime(clock.UtcNow));
    }
}
