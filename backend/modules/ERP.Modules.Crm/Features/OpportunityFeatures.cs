using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Crm.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Crm.Features;

public record OpportunityDto(
    Guid Id,
    Guid LegalEntityId,
    string OpportunityNumber,
    string Title,
    Guid? CustomerPartyId,
    string CustomerName,
    OpportunityStage Stage,
    int ProbabilityPercent,
    decimal ExpectedRevenue,
    decimal ActualRevenue,
    DateOnly? ExpectedCloseDate,
    DateOnly? ActualCloseDate,
    string? CloseReason,
    Guid? LeadId,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Notes,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record OpportunitySummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string OpportunityNumber,
    string Title,
    string CustomerName,
    OpportunityStage Stage,
    int ProbabilityPercent,
    decimal ExpectedRevenue,
    DateOnly? ExpectedCloseDate,
    string? AssignedUserName,
    DateTimeOffset CreatedAt);

public record PipelineStageSummaryDto(
    OpportunityStage Stage,
    string StageName,
    int Count,
    decimal TotalExpectedRevenue);

public record CreateOpportunityCommand(
    Guid LegalEntityId,
    string Title,
    Guid? CustomerPartyId,
    string CustomerName,
    OpportunityStage Stage,
    int ProbabilityPercent,
    decimal ExpectedRevenue,
    DateOnly? ExpectedCloseDate,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Notes,
    string? CustomFields) : ICommand<OpportunityDto>;

public record UpdateOpportunityStageCommand(
    Guid Id,
    OpportunityStage Stage,
    int ProbabilityPercent) : ICommand<OpportunityDto>;

public record CloseOpportunityCommand(
    Guid Id,
    bool IsWon,
    decimal ActualRevenue,
    string? CloseReason) : ICommand<OpportunityDto>;

public record GetOpportunityByIdQuery(Guid Id) : IQuery<OpportunityDto?>;

public record GetPipelineSummaryQuery(Guid? LegalEntityId = null) : IQuery<List<PipelineStageSummaryDto>>;

public class CreateOpportunityValidator : AbstractValidator<CreateOpportunityCommand>
{
    public CreateOpportunityValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ExpectedRevenue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProbabilityPercent).InclusiveBetween(0, 100);
    }
}

public class UpdateOpportunityStageValidator : AbstractValidator<UpdateOpportunityStageCommand>
{
    public UpdateOpportunityStageValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ProbabilityPercent).InclusiveBetween(0, 100);
    }
}

public class CloseOpportunityValidator : AbstractValidator<CloseOpportunityCommand>
{
    public CloseOpportunityValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ActualRevenue).GreaterThanOrEqualTo(0);
    }
}

public class OpportunityHandlers :
    IRequestHandler<CreateOpportunityCommand, OpportunityDto>,
    IRequestHandler<UpdateOpportunityStageCommand, OpportunityDto>,
    IRequestHandler<CloseOpportunityCommand, OpportunityDto>,
    IRequestHandler<GetOpportunityByIdQuery, OpportunityDto?>,
    IRequestHandler<GetPipelineSummaryQuery, List<PipelineStageSummaryDto>>
{
    private readonly IRepository<CrmOpportunity> _oppRepo;
    private readonly INumberingService _numbering;
    private readonly ILegalEntityGuard _guard;
    private readonly IReadRepository<ERP.Domain.Parties.Party> _parties;

    public OpportunityHandlers(
        IRepository<CrmOpportunity> oppRepo,
        INumberingService numbering,
        ILegalEntityGuard guard,
        IReadRepository<ERP.Domain.Parties.Party> parties)
    {
        _guard = guard;
        _parties = parties;
        _oppRepo = oppRepo;
        _numbering = numbering;
    }

    public async Task<OpportunityDto> Handle(CreateOpportunityCommand request, CancellationToken cancellationToken)
    {
        await _guard.RequireAsync(request.LegalEntityId, cancellationToken);
        if (request.CustomerPartyId is { } partyId && !await _parties.AnyAsync(p => p.Id == partyId, cancellationToken))
            throw new FieldValidationException("customerPartyId", "Customer not found.");
        var number = await _numbering.NextAsync("OPP", request.LegalEntityId, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);

        var opp = new CrmOpportunity
        {
            LegalEntityId = request.LegalEntityId,
            OpportunityNumber = number,
            Title = request.Title.Trim(),
            CustomerPartyId = request.CustomerPartyId,
            CustomerName = request.CustomerName.Trim(),
            Stage = request.Stage,
            ProbabilityPercent = request.ProbabilityPercent,
            ExpectedRevenue = request.ExpectedRevenue,
            ExpectedCloseDate = request.ExpectedCloseDate,
            AssignedUserId = request.AssignedUserId,
            AssignedUserName = request.AssignedUserName,
            Notes = request.Notes,
            CustomFields = request.CustomFields
        };

        _oppRepo.Add(opp);
        await _oppRepo.SaveChangesAsync(cancellationToken);
        return MapToDto(opp);
    }

    public async Task<OpportunityDto> Handle(UpdateOpportunityStageCommand request, CancellationToken cancellationToken)
    {
        var opp = await _oppRepo.Tracked().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Opportunity {request.Id} not found.");

        CrmTransitions.Opportunity.Ensure(opp.Stage, request.Stage, isSystem: false);

        opp.Stage = request.Stage;
        opp.ProbabilityPercent = request.ProbabilityPercent;

        if (request.Stage == OpportunityStage.ClosedWon)
        {
            opp.ActualCloseDate = DateOnly.FromDateTime(DateTime.UtcNow);
            opp.ActualRevenue = opp.ExpectedRevenue;
        }
        else if (request.Stage == OpportunityStage.ClosedLost)
        {
            opp.ActualCloseDate = DateOnly.FromDateTime(DateTime.UtcNow);
            opp.ActualRevenue = 0;
        }

        await _oppRepo.SaveChangesAsync(cancellationToken);
        return MapToDto(opp);
    }

    public async Task<OpportunityDto> Handle(CloseOpportunityCommand request, CancellationToken cancellationToken)
    {
        var opp = await _oppRepo.Tracked().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Opportunity {request.Id} not found.");

        if (opp.Stage == OpportunityStage.ClosedWon || opp.Stage == OpportunityStage.ClosedLost)
        {
            throw new ConflictException("Opportunity is already closed.");
        }

        opp.Stage = request.IsWon ? OpportunityStage.ClosedWon : OpportunityStage.ClosedLost;
        opp.ActualRevenue = request.IsWon ? request.ActualRevenue : 0;
        opp.ActualCloseDate = DateOnly.FromDateTime(DateTime.UtcNow);
        opp.CloseReason = request.CloseReason;
        opp.ProbabilityPercent = request.IsWon ? 100 : 0;

        await _oppRepo.SaveChangesAsync(cancellationToken);
        return MapToDto(opp);
    }

    public async Task<OpportunityDto?> Handle(GetOpportunityByIdQuery request, CancellationToken cancellationToken)
    {
        var opp = await _oppRepo.FindAsync(request.Id, cancellationToken);
        return opp is null ? null : MapToDto(opp);
    }

    public async Task<List<PipelineStageSummaryDto>> Handle(GetPipelineSummaryQuery request, CancellationToken cancellationToken)
    {
        var query = _oppRepo.Query();
        if (request.LegalEntityId.HasValue)
        {
            query = query.Where(x => x.LegalEntityId == request.LegalEntityId.Value);
        }

        var opps = await query.ToListAsync(cancellationToken);

        var stages = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.Proposal,
            OpportunityStage.Negotiation,
            OpportunityStage.ClosedWon,
            OpportunityStage.ClosedLost
        };

        return stages.Select(s => new PipelineStageSummaryDto(
            s,
            s.ToString(),
            opps.Count(x => x.Stage == s),
            opps.Where(x => x.Stage == s).Sum(x => x.ExpectedRevenue)
        )).ToList();
    }

    private static OpportunityDto MapToDto(CrmOpportunity opp) => new(
        opp.Id,
        opp.LegalEntityId,
        opp.OpportunityNumber,
        opp.Title,
        opp.CustomerPartyId,
        opp.CustomerName,
        opp.Stage,
        opp.ProbabilityPercent,
        opp.ExpectedRevenue,
        opp.ActualRevenue,
        opp.ExpectedCloseDate,
        opp.ActualCloseDate,
        opp.CloseReason,
        opp.LeadId,
        opp.AssignedUserId,
        opp.AssignedUserName,
        opp.Notes,
        opp.CustomFields,
        opp.CreatedAt);
}
