using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Services.Domain;
using FluentValidation;
using MediatR;

namespace ERP.Modules.Services.Features;

public record TimeEntryDto(
    Guid Id,
    Guid LegalEntityId,
    Guid EngagementId,
    Guid ConsultantUserId,
    string ConsultantName,
    DateOnly ActivityDate,
    decimal Hours,
    decimal HourlyRate,
    decimal TotalAmount,
    string TaskDescription,
    bool IsBillable,
    bool IsApproved,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record TimeEntrySummaryDto(
    Guid Id,
    Guid LegalEntityId,
    Guid EngagementId,
    string ConsultantName,
    DateOnly ActivityDate,
    decimal Hours,
    decimal HourlyRate,
    decimal TotalAmount,
    string TaskDescription,
    bool IsBillable,
    bool IsApproved,
    DateTimeOffset CreatedAt);

public record LogTimeEntryCommand(
    Guid LegalEntityId,
    Guid EngagementId,
    Guid ConsultantUserId,
    string ConsultantName,
    DateOnly ActivityDate,
    decimal Hours,
    decimal HourlyRate,
    string TaskDescription,
    bool IsBillable,
    string? CustomFields) : ICommand<TimeEntryDto>;

public record ApproveTimeEntryCommand(
    Guid Id,
    Guid ApprovedByUserId) : ICommand<TimeEntryDto>;

public class LogTimeEntryValidator : AbstractValidator<LogTimeEntryCommand>
{
    public LogTimeEntryValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.EngagementId).NotEmpty().WithMessage("Engagement is required.");
        RuleFor(x => x.ConsultantName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Hours).GreaterThan(0).WithMessage("Hours must be greater than zero.");
        RuleFor(x => x.HourlyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaskDescription).NotEmpty().MaximumLength(500);
    }
}

public class ApproveTimeEntryValidator : AbstractValidator<ApproveTimeEntryCommand>
{
    public ApproveTimeEntryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ApprovedByUserId).NotEmpty();
    }
}

public class TimeEntryHandlers :
    IRequestHandler<LogTimeEntryCommand, TimeEntryDto>,
    IRequestHandler<ApproveTimeEntryCommand, TimeEntryDto>
{
    private readonly IRepository<TimeEntryRecord> _timeRepo;
    private readonly IRepository<ServiceEngagement> _engagementRepo;

    public TimeEntryHandlers(
        IRepository<TimeEntryRecord> timeRepo,
        IRepository<ServiceEngagement> engagementRepo)
    {
        _timeRepo = timeRepo;
        _engagementRepo = engagementRepo;
    }

    public async Task<TimeEntryDto> Handle(LogTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var totalAmount = Math.Round(request.Hours * request.HourlyRate, 2);
        var entry = new TimeEntryRecord
        {
            LegalEntityId = request.LegalEntityId,
            EngagementId = request.EngagementId,
            ConsultantUserId = request.ConsultantUserId,
            ConsultantName = request.ConsultantName.Trim(),
            ActivityDate = request.ActivityDate,
            Hours = request.Hours,
            HourlyRate = request.HourlyRate,
            TotalAmount = totalAmount,
            TaskDescription = request.TaskDescription.Trim(),
            IsBillable = request.IsBillable,
            IsApproved = false,
            CustomFields = request.CustomFields
        };

        _timeRepo.Add(entry);

        var engagement = await _engagementRepo.FindAsync(request.EngagementId, cancellationToken);
        if (engagement is not null)
        {
            engagement.ConsumedHours += request.Hours;
            engagement.ConsumedCost += totalAmount;
        }

        await _timeRepo.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    public async Task<TimeEntryDto> Handle(ApproveTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _timeRepo.FindAsync(request.Id, cancellationToken);
        if (entry is null)
            throw new NotFoundException($"Time entry {request.Id} not found.");

        entry.IsApproved = true;
        entry.ApprovedByUserId = request.ApprovedByUserId;
        entry.ApprovedAt = DateTimeOffset.UtcNow;

        await _timeRepo.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    private static TimeEntryDto Map(TimeEntryRecord t) =>
        new(
            t.Id,
            t.LegalEntityId,
            t.EngagementId,
            t.ConsultantUserId,
            t.ConsultantName,
            t.ActivityDate,
            t.Hours,
            t.HourlyRate,
            t.TotalAmount,
            t.TaskDescription,
            t.IsBillable,
            t.IsApproved,
            t.ApprovedByUserId,
            t.ApprovedAt,
            t.CustomFields,
            t.CreatedAt);
}
