using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateEmployeeDocumentHandler(IRepository<EmployeeDocument> documents, IReadRepository<Employee> employees, ILegalEntityGuard guard, IClock clock)
    : ICommandHandler<CreateEmployeeDocumentCommand, EmployeeDocumentDto>
{
    public async Task<EmployeeDocumentDto> Handle(CreateEmployeeDocumentCommand command, CancellationToken ct)
    {
        var employee = await employees.GetAsync(command.Input.EmployeeId, ct);
        await guard.RequireAsync(employee.LegalEntityId, ct);
        var d = new EmployeeDocument { EmployeeId = employee.Id, LegalEntityId = employee.LegalEntityId };
        EmployeeDocumentMapping.Map(command.Input, d);
        documents.Add(d);
        await documents.SaveChangesAsync(ct);
        return EmployeeDocumentDto.From(d, DateOnly.FromDateTime(clock.UtcNow));
    }
}
