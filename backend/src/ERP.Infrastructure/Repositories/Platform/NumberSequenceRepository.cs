using ERP.Application.Abstractions;
using ERP.Domain.Platform;
using ERP.Modularity.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class NumberSequenceRepository(IRepository<NumberSequence> sequences) : RepositoryBase<NumberSequence>(sequences), INumberSequenceRepository
{
    public Task<List<NumberSequence>> ListWorkspaceWideAsync(CancellationToken ct)
        => Query(new Specification<NumberSequence>(s => s.LegalEntityId == null)).ToListAsync(ct);

    public Task<List<NumberSequence>> ListForDocumentTypeForUpdateAsync(string documentType, CancellationToken ct)
        => Tracked(new Specification<NumberSequence>(s => s.DocumentType == documentType)).ToListAsync(ct);
}
