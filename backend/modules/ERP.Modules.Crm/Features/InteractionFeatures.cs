using ERP.Application.Abstractions;
using ERP.Application.Common;
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
        RuleFor(x => x.RelatedEntityKind).NotEmpty();
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
    private readonly IRepository<CrmInteraction> _interactionRepo;

    public InteractionHandlers(IRepository<CrmInteraction> interactionRepo)
    {
        _interactionRepo = interactionRepo;
    }

    public async Task<CustomerInteractionDto> Handle(LogInteractionCommand request, CancellationToken cancellationToken)
    {
        var interaction = new CrmInteraction
        {
            LegalEntityId = request.LegalEntityId,
            RelatedEntityKind = request.RelatedEntityKind.Trim(),
            RelatedEntityId = request.RelatedEntityId,
            Type = request.Type,
            Subject = request.Subject.Trim(),
            Details = request.Details,
            InteractionDate = request.InteractionDate ?? DateTimeOffset.UtcNow,
            PerformedByUserId = request.PerformedByUserId,
            PerformedByName = request.PerformedByName,
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
            ?? throw new KeyNotFoundException($"Interaction {request.Id} not found.");

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
