using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateEmployeeHandler(
    IRepository<Employee> employees, ILegalEntityGuard guard, INumberingService numbers, ICustomFieldService fields, EmployeeHistoryRecorder history,
    ICurrentUser user, EmployeeDtoFactory dtos, EmployeePeople people) : ICommandHandler<CreateEmployeeCommand, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(CreateEmployeeCommand command, CancellationToken ct)
    {
        // 1. Fill the new employee: input, people links, number (unique) and custom fields.
        var i = command.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var e = new Employee { LegalEntityId = entity.Id };
        EmployeeMapping.Map(i, e, canWritePay: true);
        await people.ApplyAsync(i, e, ct);
        e.Number = string.IsNullOrWhiteSpace(i.Number) ? await numbers.NextAsync(Docs.Employee, entity.Id, null, ct) : i.Number.Trim();
        if (await employees.AnyAsync(x => x.Number == e.Number, ct)) throw new ConflictException($"Employee number '{e.Number}' is already used.");
        await fields.ApplyAsync(Docs.Employee, e, i.CustomFields, isCreate: true, ct);

        // 2. Add, record the joining and save.
        employees.Add(e);
        history.Joined(e);
        await employees.SaveChangesAsync(ct);
        return await dtos.BuildAsync(e, user.HasPermission(P.SalaryRead), ct);
    }
}
