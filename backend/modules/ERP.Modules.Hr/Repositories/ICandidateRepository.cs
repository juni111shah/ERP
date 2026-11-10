using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for recruitment candidates.</summary>
public interface ICandidateRepository : IRepository<Candidate>
{
    /// <summary>The stage of every candidate of a vacancy.</summary>
    Task<List<CandidateStage>> ListStagesOfOpeningAsync(Guid openingId, CancellationToken ct);
}
