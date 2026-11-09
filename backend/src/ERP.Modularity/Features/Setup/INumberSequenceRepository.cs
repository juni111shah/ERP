using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Modularity.Features;

/// <summary>Data access for document numbering. Implemented in ERP.Infrastructure/Repositories/Platform/NumberSequenceRepository.cs.</summary>
public interface INumberSequenceRepository : IRepository<NumberSequence>
{
    /// <summary>The workspace-wide sequences (no legal entity), every year, read-only.</summary>
    Task<List<NumberSequence>> ListWorkspaceWideAsync(CancellationToken ct);

    /// <summary>Every sequence of <paramref name="documentType"/> (all years and legal entities), tracked for changes.</summary>
    Task<List<NumberSequence>> ListForDocumentTypeForUpdateAsync(string documentType, CancellationToken ct);
}
