using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteEmployeeDocumentHandler(IRepository<EmployeeDocument> documents) : ICommandHandler<DeleteEmployeeDocumentCommand>
{
    public async Task<Unit> Handle(DeleteEmployeeDocumentCommand command, CancellationToken ct)
    {
        documents.Remove(await documents.GetForUpdateAsync(command.Id, ct));
        await documents.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
