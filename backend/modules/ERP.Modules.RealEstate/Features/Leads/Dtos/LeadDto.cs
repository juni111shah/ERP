using System.Linq.Expressions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record LeadInput(Guid LegalEntityId, string Name, string? Phone, string? Email, string? Source, string? Interest, Guid? AgentPartyId, Guid? ListingId, Guid? UnitId, string? Notes, DateOnly? FollowUpOn);

public sealed record LeadDto(Guid Id, Guid LegalEntityId, string Name, string? Phone, string? Email, string? Source, string? Interest, Guid? AgentPartyId, LeadStage Stage, string? LostReason, Guid? ListingId, Guid? UnitId, string? Notes, DateOnly? FollowUpOn, Guid? DealId)
{
    public static LeadDto From(Lead l) => new(l.Id, l.LegalEntityId, l.Name, l.Phone, l.Email, l.Source, l.Interest, l.AgentPartyId, l.Stage, l.LostReason, l.ListingId, l.UnitId, l.Notes, l.FollowUpOn, l.DealId);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Lead, LeadDto>> Projection = x => new LeadDto(x.Id, x.LegalEntityId, x.Name, x.Phone, x.Email, x.Source, x.Interest, x.AgentPartyId, x.Stage, x.LostReason, x.ListingId, x.UnitId, x.Notes, x.FollowUpOn, x.DealId);
}

public sealed record PipelineStageDto(LeadStage Stage, int Count);
public sealed record LeadPipelineDto(IReadOnlyList<PipelineStageDto> Stages, int Open, int Won, int Lost, int FollowUpsDue, decimal WinRatePercent);
public enum LeadConvertTo { Sale = 1, Lease = 2 }
public sealed record ConvertLeadInput(LeadConvertTo? DealType = null, decimal? DealValue = null, decimal? CommissionRatePercent = null, Guid? ClientPartyId = null);
