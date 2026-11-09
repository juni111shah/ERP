using ERP.Application.Common;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class GetLeadByIdHandler(ICrmLeadRepository leads) : IQueryHandler<GetLeadByIdQuery, LeadDto?>
{
    public async Task<LeadDto?> Handle(GetLeadByIdQuery request, CancellationToken cancellationToken)
    {
        var lead = await leads.FindAsync(request.Id, cancellationToken);
        return lead is null ? null : LeadDto.From(lead);
    }
}
