using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateLeadHandler(IRepository<Lead> leads, LeadInputRules inputs, ILegalEntityGuard guard) : ICommandHandler<CreateLeadCommand, LeadDto>
{
    public async Task<LeadDto> Handle(CreateLeadCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.Input.LegalEntityId, ct);
        var l = new Lead { LegalEntityId = cmd.Input.LegalEntityId };
        await inputs.MapAsync(cmd.Input, l, ct);
        leads.Add(l);
        await leads.SaveChangesAsync(ct);
        return LeadDto.From(l);
    }
}
