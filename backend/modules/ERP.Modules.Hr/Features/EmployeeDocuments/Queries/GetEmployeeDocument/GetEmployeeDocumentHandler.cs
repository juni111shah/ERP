using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class GetEmployeeDocumentHandler(IReadRepository<EmployeeDocument> documents, IClock clock) : IQueryHandler<GetEmployeeDocumentQuery, EmployeeDocumentDto>
{
    public async Task<EmployeeDocumentDto> Handle(GetEmployeeDocumentQuery query, CancellationToken ct)
        => EmployeeDocumentDto.From(await documents.GetAsync(query.Id, ct), DateOnly.FromDateTime(clock.UtcNow));
}
