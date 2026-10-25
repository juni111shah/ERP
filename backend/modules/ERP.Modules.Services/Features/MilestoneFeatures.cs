using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Services.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

public record InvoiceMilestoneCommand(Guid Id) : ICommand<InvoiceDto>;

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

public class InvoiceMilestoneValidator : AbstractValidator<InvoiceMilestoneCommand>
{
    public InvoiceMilestoneValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class MilestoneHandlers :
    IRequestHandler<CreateMilestoneCommand, MilestoneDto>,
    IRequestHandler<UpdateMilestoneStatusCommand, MilestoneDto>,
    IRequestHandler<InvoiceMilestoneCommand, InvoiceDto>
{
    private readonly IRepository<BillingMilestone> _repo;
    private readonly IReadRepository<ServiceEngagement> _engagements;
    private readonly INumberingService _numbering;
    private readonly ISender _sender;

    public MilestoneHandlers(
        IRepository<BillingMilestone> repo,
        IReadRepository<ServiceEngagement> engagements,
        INumberingService numbering,
        ISender sender)
    {
        _repo = repo;
        _engagements = engagements;
        _numbering = numbering;
        _sender = sender;
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
        var milestone = await _repo.Tracked().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Billing milestone {request.Id} not found.");

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

    public async Task<InvoiceDto> Handle(InvoiceMilestoneCommand request, CancellationToken cancellationToken)
    {
        var milestone = await _repo.Tracked().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Billing milestone {request.Id} not found.");

        if (milestone.Status == MilestoneStatus.Invoiced && milestone.InvoiceId.HasValue)
        {
            return await _sender.Send(new GetInvoice(milestone.InvoiceId.Value), cancellationToken);
        }

        var engagement = await _engagements.FindAsync(milestone.EngagementId, cancellationToken)
            ?? throw new NotFoundException($"Service engagement {milestone.EngagementId} not found.");

        if (engagement.Status == EngagementStatus.Cancelled)
        {
            throw new ConflictException("Cannot invoice milestone for a cancelled service engagement.");
        }

        if (engagement.ClientId == Guid.Empty)
        {
            throw new ConflictException("Service engagement must have a valid client to generate an invoice.");
        }

        var invoice = await _sender.Send(new CreateInvoice(new InvoiceInput(
            LegalEntityId: milestone.LegalEntityId,
            PartyId: engagement.ClientId,
            InvoiceDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate: milestone.DueDate,
            Lines: [
                new InvoiceLineInput(
                    $"{milestone.MilestoneCode}: {milestone.Title} ({engagement.Title})",
                    1,
                    milestone.Amount)
            ],
            Notes: $"Milestone billing for {engagement.EngagementCode}: {engagement.Title}")), cancellationToken);

        milestone.Status = MilestoneStatus.Invoiced;
        milestone.InvoiceId = invoice.Id;
        milestone.CompletionDate ??= DateOnly.FromDateTime(DateTime.UtcNow);

        await _repo.SaveChangesAsync(cancellationToken);
        return invoice;
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
