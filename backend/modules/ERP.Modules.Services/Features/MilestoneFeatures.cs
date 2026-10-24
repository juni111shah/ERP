using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Services.Domain;
using FluentValidation;
using MediatR;

namespace ERP.Modules.Services.Features;

public record MilestoneDto(
    Guid Id,
    Guid LegalEntityId,
    string MilestoneCode,
    Guid EngagementId,
    string Title,
    decimal Amount,
    DateOnly DueDate,
    DateOnly? CompletionDate,
    MilestoneStatus Status,
    Guid? InvoiceId,
    string? Notes,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record MilestoneSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string MilestoneCode,
    Guid EngagementId,
    string Title,
    decimal Amount,
    DateOnly DueDate,
    DateOnly? CompletionDate,
    MilestoneStatus Status,
    DateTimeOffset CreatedAt);

public record CreateMilestoneCommand(
    Guid LegalEntityId,
    Guid EngagementId,
    string Title,
    decimal Amount,
    DateOnly DueDate,
    string? Notes,
    string? CustomFields) : ICommand<MilestoneDto>;

public record UpdateMilestoneStatusCommand(
    Guid Id,
    MilestoneStatus Status,
    Guid? InvoiceId) : ICommand<MilestoneDto>;

public class CreateMilestoneValidator : AbstractValidator<CreateMilestoneCommand>
{
    public CreateMilestoneValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.EngagementId).NotEmpty().WithMessage("Engagement is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
    }
}

public class UpdateMilestoneStatusValidator : AbstractValidator<UpdateMilestoneStatusCommand>
{
    public UpdateMilestoneStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class MilestoneHandlers :
    IRequestHandler<CreateMilestoneCommand, MilestoneDto>,
    IRequestHandler<UpdateMilestoneStatusCommand, MilestoneDto>
{
    private readonly IRepository<BillingMilestone> _repo;
    private readonly INumberingService _numbering;

    public MilestoneHandlers(IRepository<BillingMilestone> repo, INumberingService numbering)
    {
        _repo = repo;
        _numbering = numbering;
    }

    public async Task<MilestoneDto> Handle(CreateMilestoneCommand request, CancellationToken cancellationToken)
    {
        var code = await _numbering.NextAsync("MLS", request.LegalEntityId, request.DueDate, cancellationToken);
        var milestone = new BillingMilestone
        {
            LegalEntityId = request.LegalEntityId,
            MilestoneCode = code,
            EngagementId = request.EngagementId,
            Title = request.Title.Trim(),
            Amount = request.Amount,
            DueDate = request.DueDate,
            Status = MilestoneStatus.Planned,
            Notes = request.Notes?.Trim(),
            CustomFields = request.CustomFields
        };

        _repo.Add(milestone);
        await _repo.SaveChangesAsync(cancellationToken);

        return Map(milestone);
    }

    public async Task<MilestoneDto> Handle(UpdateMilestoneStatusCommand request, CancellationToken cancellationToken)
    {
        var milestone = await _repo.FindAsync(request.Id, cancellationToken);
        if (milestone is null)
            throw new KeyNotFoundException($"Billing milestone {request.Id} not found.");

        milestone.Status = request.Status;
        if (request.Status == MilestoneStatus.ReadyToInvoice && milestone.CompletionDate is null)
        {
            milestone.CompletionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        if (request.InvoiceId.HasValue)
        {
            milestone.InvoiceId = request.InvoiceId.Value;
        }

        await _repo.SaveChangesAsync(cancellationToken);
        return Map(milestone);
    }

    private static MilestoneDto Map(BillingMilestone m) =>
        new(
            m.Id,
            m.LegalEntityId,
            m.MilestoneCode,
            m.EngagementId,
            m.Title,
            m.Amount,
            m.DueDate,
            m.CompletionDate,
            m.Status,
            m.InvoiceId,
            m.Notes,
            m.CustomFields,
            m.CreatedAt);
}
