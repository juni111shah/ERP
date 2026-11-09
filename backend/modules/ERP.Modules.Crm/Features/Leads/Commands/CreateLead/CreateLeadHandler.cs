using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class CreateLeadHandler(ICrmLeadRepository leads, INumberingService numbering, ILegalEntityGuard guard) : ICommandHandler<CreateLeadCommand, LeadDto>
{
    public async Task<LeadDto> Handle(CreateLeadCommand request, CancellationToken cancellationToken)
    {
        // 1. The caller must have access to the legal entity; the lead takes its next LEAD number.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var number = await numbering.NextAsync("LEAD", request.LegalEntityId, DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), cancellationToken);

        // 2. A new lead always starts as New.
        var lead = new CrmLead
        {
            LegalEntityId = request.LegalEntityId,
            LeadNumber = number,
            FullName = request.FullName.Trim(),
            CompanyName = request.CompanyName?.Trim(),
            JobTitle = request.JobTitle?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            Source = request.Source,
            Status = LeadStatus.New,
            EstimatedValue = request.EstimatedValue,
            AssignedUserId = request.AssignedUserId,
            AssignedUserName = request.AssignedUserName,
            Notes = request.Notes,
            CustomFields = request.CustomFields
        };

        // 3. Save.
        leads.Add(lead);
        await leads.SaveChangesAsync(cancellationToken);

        return LeadDto.From(lead);
    }
}
