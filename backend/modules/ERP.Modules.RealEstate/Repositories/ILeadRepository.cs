using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for leads. Implemented in LeadRepository.cs.</summary>
public interface ILeadRepository : IRepository<Lead>
{
    /// <summary>Stage and follow-up date of the leads of one agent or (null) all.</summary>
    Task<List<LeadStageRow>> ListStagesAsync(Guid? agentPartyId, CancellationToken ct);
}

/// <summary>A lead's stage and follow-up date.</summary>
public sealed record LeadStageRow(LeadStage Stage, DateOnly? FollowUpOn);
