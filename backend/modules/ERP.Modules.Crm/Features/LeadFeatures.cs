using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Crm.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Crm.Features;

public record LeadDto(
    Guid Id,
    Guid LegalEntityId,
    string LeadNumber,
    string FullName,
    string? CompanyName,
    string? JobTitle,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadStatus Status,
    decimal EstimatedValue,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Notes,
    Guid? ConvertedPartyId,
    Guid? ConvertedOpportunityId,
    DateTimeOffset? ConvertedAt,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record LeadSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string LeadNumber,
    string FullName,
    string? CompanyName,
    string? JobTitle,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadStatus Status,
    decimal EstimatedValue,
    string? AssignedUserName,
    string? Notes,
    DateTimeOffset CreatedAt);

public record CreateLeadCommand(
    Guid LegalEntityId,
    string FullName,
    string? CompanyName,
    string? JobTitle,
    string? Email,
    string? Phone,
    LeadSource Source,
    decimal EstimatedValue,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Notes,
    string? CustomFields) : ICommand<LeadDto>;

public record UpdateLeadCommand(
    Guid Id,
    string FullName,
    string? CompanyName,
    string? JobTitle,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadStatus Status,
    decimal EstimatedValue,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Notes,
    string? CustomFields) : ICommand<LeadDto>;

public record ConvertLeadCommand(
    Guid Id,
    string OpportunityTitle,
    decimal ExpectedRevenue,
    DateOnly? ExpectedCloseDate) : ICommand<Guid>;

public record GetLeadByIdQuery(Guid Id) : IQuery<LeadDto?>;

public class CreateLeadValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150).WithMessage("Full name is required.");
        RuleFor(x => x.EstimatedValue).GreaterThanOrEqualTo(0);
        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email).EmailAddress().MaximumLength(150);
        });
    }
}

public class UpdateLeadValidator : AbstractValidator<UpdateLeadCommand>
{
    public UpdateLeadValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.EstimatedValue).GreaterThanOrEqualTo(0);
    }
}

public class ConvertLeadValidator : AbstractValidator<ConvertLeadCommand>
{
    public ConvertLeadValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.OpportunityTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExpectedRevenue).GreaterThanOrEqualTo(0);
    }
}

public class LeadHandlers :
    IRequestHandler<CreateLeadCommand, LeadDto>,
    IRequestHandler<UpdateLeadCommand, LeadDto>,
    IRequestHandler<ConvertLeadCommand, Guid>,
    IRequestHandler<GetLeadByIdQuery, LeadDto?>
{
    private readonly IRepository<CrmLead> _leadRepo;
    private readonly IRepository<CrmOpportunity> _oppRepo;
    private readonly INumberingService _numbering;

    public LeadHandlers(
        IRepository<CrmLead> leadRepo,
        IRepository<CrmOpportunity> oppRepo,
        INumberingService numbering)
    {
        _leadRepo = leadRepo;
        _oppRepo = oppRepo;
        _numbering = numbering;
    }

    public async Task<LeadDto> Handle(CreateLeadCommand request, CancellationToken cancellationToken)
    {
        var number = await _numbering.NextAsync("LEAD", request.LegalEntityId, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);

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

        _leadRepo.Add(lead);
        await _leadRepo.SaveChangesAsync(cancellationToken);
        return MapToDto(lead);
    }

    public async Task<LeadDto> Handle(UpdateLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = await _leadRepo.FindAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Lead {request.Id} not found.");

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

        await _leadRepo.SaveChangesAsync(cancellationToken);
        return MapToDto(lead);
    }

    public async Task<Guid> Handle(ConvertLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = await _leadRepo.FindAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Lead {request.Id} not found.");

        var oppNumber = await _numbering.NextAsync("OPP", lead.LegalEntityId, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);

        var opp = new CrmOpportunity
        {
            LegalEntityId = lead.LegalEntityId,
            OpportunityNumber = oppNumber,
            Title = request.OpportunityTitle.Trim(),
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

        _oppRepo.Add(opp);
        await _oppRepo.SaveChangesAsync(cancellationToken);

        lead.Status = LeadStatus.Converted;
        lead.ConvertedOpportunityId = opp.Id;
        lead.ConvertedAt = DateTimeOffset.UtcNow;
        await _leadRepo.SaveChangesAsync(cancellationToken);

        return opp.Id;
    }

    public async Task<LeadDto?> Handle(GetLeadByIdQuery request, CancellationToken cancellationToken)
    {
        var lead = await _leadRepo.FindAsync(request.Id, cancellationToken);
        return lead is null ? null : MapToDto(lead);
    }

    private static LeadDto MapToDto(CrmLead lead) => new(
        lead.Id,
        lead.LegalEntityId,
        lead.LeadNumber,
        lead.FullName,
        lead.CompanyName,
        lead.JobTitle,
        lead.Email,
        lead.Phone,
        lead.Source,
        lead.Status,
        lead.EstimatedValue,
        lead.AssignedUserId,
        lead.AssignedUserName,
        lead.Notes,
        lead.ConvertedPartyId,
        lead.ConvertedOpportunityId,
        lead.ConvertedAt,
        lead.CustomFields,
        lead.CreatedAt);
}
