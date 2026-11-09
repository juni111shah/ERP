using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateEmployeeDocumentHandler(IRepository<EmployeeDocument> documents, IClock clock) : ICommandHandler<UpdateEmployeeDocumentCommand, EmployeeDocumentDto>
{
    public async Task<EmployeeDocumentDto> Handle(UpdateEmployeeDocumentCommand command, CancellationToken ct)
    {
        var d = await documents.GetForUpdateAsync(command.Id, ct);
        if (command.Input.EmployeeId != d.EmployeeId) throw new FieldValidationException("employeeId", "A document cannot move to another employee.");
        EmployeeDocumentMapping.Map(command.Input, d);
        await documents.SaveChangesAsync(ct);
        return EmployeeDocumentDto.From(d, DateOnly.FromDateTime(clock.UtcNow));
    }
}
