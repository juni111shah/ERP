using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateLeadHandler(IRepository<Lead> leads, LeadInputRules inputs) : ICommandHandler<UpdateLeadCommand, LeadDto>
{
    public async Task<LeadDto> Handle(UpdateLeadCommand cmd, CancellationToken ct)
    {
        var l = await leads.GetForUpdateAsync(cmd.Id, ct);
        if (l.Stage == LeadStage.Won) throw new ConflictException("A won lead can no longer be edited.");
        if (cmd.Input.LegalEntityId != l.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of a lead cannot change.");
        await inputs.MapAsync(cmd.Input, l, ct);
        await leads.SaveChangesAsync(ct);
        return LeadDto.From(l);
    }
}
