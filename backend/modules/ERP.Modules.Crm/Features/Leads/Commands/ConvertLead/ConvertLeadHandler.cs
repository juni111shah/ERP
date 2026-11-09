using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class ConvertLeadHandler(
    ICrmLeadRepository leads,
    ICrmOpportunityRepository opportunities,
    IRepository<ERP.Domain.Parties.Party> parties,
    INumberingService numbering,
    IUnitOfWork uow) : ICommandHandler<ConvertLeadCommand, Guid>
{
    // The customer, the opportunity and the lead's new status commit together.
    public Task<Guid> Handle(ConvertLeadCommand request, CancellationToken cancellationToken)
        => uow.InTransactionAsync(token => ConvertAsync(request, token), cancellationToken);

    private async Task<Guid> ConvertAsync(ConvertLeadCommand request, CancellationToken cancellationToken)
    {
        // 1. Load the lead; it must be convertible.
        var lead = await leads.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Lead {request.Id} not found.");

        if (lead.Status == LeadStatus.Converted || lead.ConvertedOpportunityId.HasValue) throw new ConflictException("This lead is already converted.");
        if (lead.Status == LeadStatus.Unqualified) throw new ConflictException("An unqualified lead cannot be converted. Requalify it first.");

        // 2. Create the customer, unless an earlier attempt already did.
        if (!lead.ConvertedPartyId.HasValue)
        {
            var isCompany = !string.IsNullOrWhiteSpace(lead.CompanyName);
            var customerName = isCompany ? lead.CompanyName!.Trim() : lead.FullName.Trim();
            var partyCode = await numbering.NextAsync("party", ct: cancellationToken); // the core party sequence; there is no "CUST" sequence

            var party = new ERP.Domain.Parties.Party
            {
                Code = partyCode,
                Name = customerName,
                Kind = isCompany ? ERP.Domain.Parties.PartyKind.Company : ERP.Domain.Parties.PartyKind.Individual,
                Roles = ERP.Domain.Parties.PartyRoles.Customer,
                Email = lead.Email,
                Phone = lead.Phone,
                IsActive = true
            };

            parties.Add(party);
            await parties.SaveChangesAsync(cancellationToken);

            lead.ConvertedPartyId = party.Id;
        }

        // 3. Create the opportunity.
        var oppNumber = await numbering.NextAsync("OPP", lead.LegalEntityId, DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), cancellationToken);

        var opp = new CrmOpportunity
        {
            LegalEntityId = lead.LegalEntityId,
            OpportunityNumber = oppNumber,
            Title = request.OpportunityTitle.Trim(),
            CustomerPartyId = lead.ConvertedPartyId,
            CustomerName = lead.CompanyName ?? lead.FullName,
            Stage = OpportunityStage.Qualification,
            ProbabilityPercent = 25,
            ExpectedRevenue = request.ExpectedRevenue,
            ExpectedCloseDate = request.ExpectedCloseDate,
            LeadId = lead.Id,
            AssignedUserId = lead.AssignedUserId,
            AssignedUserName = lead.AssignedUserName,
            Notes = $"Converted from Lead {lead.LeadNumber}: {lead.Notes}"
        };

        opportunities.Add(opp);
        await opportunities.SaveChangesAsync(cancellationToken);

        // 4. Mark the lead converted.
        lead.Status = LeadStatus.Converted;
        lead.ConvertedOpportunityId = opp.Id;
        lead.ConvertedAt = DateTimeOffset.UtcNow;
        await leads.SaveChangesAsync(cancellationToken);

        return opp.Id;
    }
}
