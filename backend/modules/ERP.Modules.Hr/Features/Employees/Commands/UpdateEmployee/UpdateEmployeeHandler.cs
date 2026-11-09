using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateEmployeeHandler(
    IRepository<Employee> employees, ICustomFieldService fields, EmployeeHistoryRecorder history, ICurrentUser user, EmployeeDtoFactory dtos, EmployeePeople people)
    : ICommandHandler<UpdateEmployeeCommand, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(UpdateEmployeeCommand command, CancellationToken ct)
    {
        // 1. Load it; the employing entity stays, a changed number must still be unique.
        var e = await employees.GetForUpdateAsync(command.Id, ct);
        var i = command.Input;
        if (i.LegalEntityId != e.LegalEntityId) throw new FieldValidationException("legalEntityId", "An employee cannot move to another employing entity here.");
        var number = string.IsNullOrWhiteSpace(i.Number) ? e.Number : i.Number.Trim();
        if (number != e.Number && await employees.AnyAsync(x => x.Number == number && x.Id != e.Id, ct)) throw new ConflictException($"Employee number '{number}' is already used.");

        // 2. Apply the input, the people links and the custom fields.
        var before = EmployeeSnapshot.Of(e);
        EmployeeMapping.Map(i, e, canWritePay: user.HasPermission(P.SalaryRead)); e.Number = number;
        await people.ApplyAsync(i, e, ct);
        await fields.ApplyAsync(Docs.Employee, e, i.CustomFields, isCreate: false, ct);

        // 3. Record what changed and save.
        history.Changed(e, before);
        await employees.SaveChangesAsync(ct);
        return await dtos.BuildAsync(e, user.HasPermission(P.SalaryRead), ct);
    }
}
