using ERP.Application.Common;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class GetOpportunityByIdHandler(ICrmOpportunityRepository opportunities) : IQueryHandler<GetOpportunityByIdQuery, OpportunityDto?>
{
    public async Task<OpportunityDto?> Handle(GetOpportunityByIdQuery request, CancellationToken cancellationToken)
    {
        var opp = await opportunities.FindAsync(request.Id, cancellationToken);
        return opp is null ? null : OpportunityDto.From(opp);
    }
}
