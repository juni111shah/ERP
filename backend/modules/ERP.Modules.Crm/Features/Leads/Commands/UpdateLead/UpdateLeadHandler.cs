using ERP.Application.Common;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class UpdateLeadHandler(ICrmLeadRepository leads) : ICommandHandler<UpdateLeadCommand, LeadDto>
{
    public async Task<LeadDto> Handle(UpdateLeadCommand request, CancellationToken cancellationToken)
    {
        // 1. Load the lead.
        var lead = await leads.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Lead {request.Id} not found.");

        // 2. A converted lead is final, and only converting it makes it Converted.
        if (lead.Status == LeadStatus.Converted) throw new ConflictException("A converted lead cannot be edited.");
        if (request.Status == LeadStatus.Converted) throw new FieldValidationException("status", "A lead becomes Converted only by converting it to an opportunity.");

        // 3. Apply the changes and save.
        lead.FullName = request.FullName.Trim();
        lead.CompanyName = request.CompanyName?.Trim();
        lead.JobTitle = request.JobTitle?.Trim();
        lead.Email = request.Email?.Trim();
        lead.Phone = request.Phone?.Trim();
        lead.Source = request.Source;
        lead.Status = request.Status;
        lead.EstimatedValue = request.EstimatedValue;
        lead.AssignedUserId = request.AssignedUserId;
        lead.AssignedUserName = request.AssignedUserName;
        lead.Notes = request.Notes;
        lead.CustomFields = request.CustomFields;

        await leads.SaveChangesAsync(cancellationToken);
        return LeadDto.From(lead);
    }
}
