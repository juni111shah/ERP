using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetLeadPipelineHandler(ILeadRepository leads, IClock clock) : IQueryHandler<GetLeadPipelineQuery, LeadPipelineDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<LeadPipelineDto> Handle(GetLeadPipelineQuery q, CancellationToken ct)
    {
        var rows = await leads.ListStagesAsync(q.AgentPartyId, ct);
        var stages = Enum.GetValues<LeadStage>().Select(s => new PipelineStageDto(s, rows.Count(r => r.Stage == s))).ToList();
        var won = rows.Count(r => r.Stage == LeadStage.Won); var lost = rows.Count(r => r.Stage == LeadStage.Lost);
        var due = rows.Count(r => r.Stage is not (LeadStage.Won or LeadStage.Lost) && r.FollowUpOn is { } d && d <= Today);
        return new(stages, rows.Count - won - lost, won, lost, due, won + lost == 0 ? 0 : Math.Round(won * 100m / (won + lost), 1));
    }
}
