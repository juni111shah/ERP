using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Crm.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Crm.Features;

public record CustomerInteractionDto(
    Guid Id,
    Guid LegalEntityId,
    string RelatedEntityKind,
    Guid RelatedEntityId,
    InteractionType Type,
    string Subject,
    string? Details,
    DateTimeOffset InteractionDate,
    Guid? PerformedByUserId,
    string? PerformedByName,
    DateTimeOffset? FollowUpDate,
    bool IsResolved,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record LogInteractionCommand(
    Guid LegalEntityId,
    string RelatedEntityKind,
    Guid RelatedEntityId,
    InteractionType Type,
    string Subject,
    string? Details,
    DateTimeOffset? InteractionDate,
    Guid? PerformedByUserId,
    string? PerformedByName,
    DateTimeOffset? FollowUpDate,
    bool IsResolved,
    string? CustomFields) : ICommand<CustomerInteractionDto>;

public record ResolveInteractionCommand(Guid Id) : ICommand<bool>;

public record GetInteractionsByRelatedEntityQuery(
    string RelatedEntityKind,
    Guid RelatedEntityId) : IQuery<List<CustomerInteractionDto>>;

public class LogInteractionValidator : AbstractValidator<LogInteractionCommand>
{
    public LogInteractionValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RelatedEntityKind).NotEmpty().Must(k => InteractionHandlers.Kinds.Contains(k?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
            .WithMessage("The related record must be a Lead, Opportunity or Customer.");
        RuleFor(x => x.RelatedEntityId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
    }
}

public class ResolveInteractionValidator : AbstractValidator<ResolveInteractionCommand>
{
    public ResolveInteractionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class InteractionHandlers :
    IRequestHandler<LogInteractionCommand, CustomerInteractionDto>,
    IRequestHandler<ResolveInteractionCommand, bool>,
    IRequestHandler<GetInteractionsByRelatedEntityQuery, List<CustomerInteractionDto>>
{
    public static readonly string[] Kinds = ["Lead", "Opportunity", "Customer"];

    private readonly IRepository<CrmInteraction> _interactionRepo;
    private readonly IReadRepository<CrmLead> _leads;
    private readonly IReadRepository<CrmOpportunity> _opportunities;
    private readonly IReadRepository<ERP.Domain.Parties.Party> _parties;
    private readonly ILegalEntityGuard _guard;
    private readonly ICurrentUser _user;

    public InteractionHandlers(
        IRepository<CrmInteraction> interactionRepo,
        IReadRepository<CrmLead> leads,
        IReadRepository<CrmOpportunity> opportunities,
        IReadRepository<ERP.Domain.Parties.Party> parties,
        ILegalEntityGuard guard,
        ICurrentUser user)
    {
        _interactionRepo = interactionRepo;
        _leads = leads;
        _opportunities = opportunities;
        _parties = parties;
        _guard = guard;
        _user = user;
    }

    public async Task<CustomerInteractionDto> Handle(LogInteractionCommand request, CancellationToken cancellationToken)
    {
        await _guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var kind = Kinds.First(k => string.Equals(k, request.RelatedEntityKind.Trim(), StringComparison.OrdinalIgnoreCase));
        var id = request.RelatedEntityId;
        var found = kind switch
        {
            "Lead" => await _leads.AnyAsync(l => l.Id == id && l.LegalEntityId == request.LegalEntityId, cancellationToken),
            "Opportunity" => await _opportunities.AnyAsync(o => o.Id == id && o.LegalEntityId == request.LegalEntityId, cancellationToken),
            _ => await _parties.AnyAsync(p => p.Id == id, cancellationToken),
        };
        if (!found) throw new FieldValidationException("relatedEntityId", $"{kind} not found.");

        var interaction = new CrmInteraction
        {
            LegalEntityId = request.LegalEntityId,
            RelatedEntityKind = kind,
            RelatedEntityId = request.RelatedEntityId,
            Type = request.Type,
            Subject = request.Subject.Trim(),
            Details = request.Details,
            InteractionDate = request.InteractionDate ?? DateTimeOffset.UtcNow,
            PerformedByUserId = _user.UserId, // the caller, never a value sent in the request
            PerformedByName = _user.Email,
            FollowUpDate = request.FollowUpDate,
            IsResolved = request.IsResolved,
            CustomFields = request.CustomFields
        };

        _interactionRepo.Add(interaction);
        await _interactionRepo.SaveChangesAsync(cancellationToken);
        return MapToDto(interaction);
    }

    public async Task<bool> Handle(ResolveInteractionCommand request, CancellationToken cancellationToken)
    {
        var interaction = await _interactionRepo.FindAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Interaction {request.Id} not found.");

        interaction.IsResolved = true;
        await _interactionRepo.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<CustomerInteractionDto>> Handle(GetInteractionsByRelatedEntityQuery request, CancellationToken cancellationToken)
    {
        var list = await _interactionRepo.Query()
            .Where(x => x.RelatedEntityKind == request.RelatedEntityKind && x.RelatedEntityId == request.RelatedEntityId)
            .OrderByDescending(x => x.InteractionDate)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    private static CustomerInteractionDto MapToDto(CrmInteraction i) => new(
        i.Id,
        i.LegalEntityId,
        i.RelatedEntityKind,
        i.RelatedEntityId,
        i.Type,
        i.Subject,
        i.Details,
        i.InteractionDate,
        i.PerformedByUserId,
        i.PerformedByName,
        i.FollowUpDate,
        i.IsResolved,
        i.CustomFields,
        i.CreatedAt);
}
