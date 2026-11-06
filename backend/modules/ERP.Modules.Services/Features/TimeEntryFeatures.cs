using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using Microsoft.EntityFrameworkCore;
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
    DateTimeOffset CreatedAt,
    Guid? InvoiceId = null);

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
    DateTimeOffset CreatedAt,
    Guid? InvoiceId = null);

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

/// <summary>What can be corrected on an entry before approval. The engagement stays: to move time, delete the entry and log it on the right engagement. A null consultant keeps the current one.</summary>
public record UpdateTimeEntryCommand(
    Guid Id,
    DateOnly ActivityDate,
    decimal Hours,
    decimal HourlyRate,
    string TaskDescription,
    bool IsBillable,
    Guid? ConsultantUserId = null,
    string? ConsultantName = null,
    string? CustomFields = null) : ICommand<TimeEntryDto>;

public record DeleteTimeEntryCommand(Guid Id) : ICommand;

/// <summary>The approver is always the signed-in user; ApprovedByUserId is accepted for older clients and ignored.</summary>
public record ApproveTimeEntryCommand(
    Guid Id,
    Guid? ApprovedByUserId = null) : ICommand<TimeEntryDto>;

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

public class UpdateTimeEntryValidator : AbstractValidator<UpdateTimeEntryCommand>
{
    public UpdateTimeEntryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ConsultantName).MaximumLength(150);
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
    }
}

public class TimeEntryHandlers :
    IRequestHandler<LogTimeEntryCommand, TimeEntryDto>,
    IRequestHandler<UpdateTimeEntryCommand, TimeEntryDto>,
    IRequestHandler<DeleteTimeEntryCommand, Unit>,
    IRequestHandler<ApproveTimeEntryCommand, TimeEntryDto>
{
    private readonly IRepository<TimeEntryRecord> _timeRepo;
    private readonly IRepository<ServiceEngagement> _engagementRepo;
    private readonly ILegalEntityGuard _guard;
    private readonly ICurrentUser _user;

    public TimeEntryHandlers(
        IRepository<TimeEntryRecord> timeRepo,
        IRepository<ServiceEngagement> engagementRepo,
        ILegalEntityGuard guard,
        ICurrentUser user)
    {
        _guard = guard;
        _timeRepo = timeRepo;
        _engagementRepo = engagementRepo;
        _user = user;
    }

    public async Task<TimeEntryDto> Handle(LogTimeEntryCommand request, CancellationToken cancellationToken)
    {
        await _guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var engagement = await _engagementRepo.Tracked().FirstOrDefaultAsync(x => x.Id == request.EngagementId, cancellationToken)
            ?? throw new FieldValidationException("engagementId", "Engagement not found.");
        if (engagement.LegalEntityId != request.LegalEntityId)
            throw new FieldValidationException("engagementId", "The engagement belongs to another legal entity.");
        EnsureTakesTime(engagement);

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
        engagement.ConsumedHours += request.Hours;
        engagement.ConsumedCost += totalAmount;

        await _timeRepo.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    public async Task<TimeEntryDto> Handle(UpdateTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await LoadUnapprovedAsync(request.Id, "corrected", cancellationToken);
        var engagement = await _engagementRepo.Tracked().FirstAsync(x => x.Id == entry.EngagementId, cancellationToken);
        EnsureTakesTime(engagement);

        var totalAmount = Math.Round(request.Hours * request.HourlyRate, 2);
        engagement.ConsumedHours += request.Hours - entry.Hours;
        engagement.ConsumedCost += totalAmount - entry.TotalAmount;

        entry.ActivityDate = request.ActivityDate;
        entry.Hours = request.Hours;
        entry.HourlyRate = request.HourlyRate;
        entry.TotalAmount = totalAmount;
        entry.TaskDescription = request.TaskDescription.Trim();
        entry.IsBillable = request.IsBillable;
        entry.ConsultantUserId = request.ConsultantUserId ?? entry.ConsultantUserId;
        if (!string.IsNullOrWhiteSpace(request.ConsultantName)) entry.ConsultantName = request.ConsultantName.Trim();
        entry.CustomFields = request.CustomFields ?? entry.CustomFields;

        await _timeRepo.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    public async Task<Unit> Handle(DeleteTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await LoadUnapprovedAsync(request.Id, "deleted", cancellationToken);
        var engagement = await _engagementRepo.Tracked().FirstOrDefaultAsync(x => x.Id == entry.EngagementId, cancellationToken);
        if (engagement is not null)
        {
            engagement.ConsumedHours -= entry.Hours;
            engagement.ConsumedCost -= entry.TotalAmount;
        }
        _timeRepo.Remove(entry);
        await _timeRepo.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<TimeEntryDto> Handle(ApproveTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _timeRepo.Tracked().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Time entry {request.Id} not found.");
        if (entry.IsApproved) throw new ConflictException("This time entry is already approved.");
        if (_user.UserId is not { } approver) throw new ForbiddenException("Approving time needs a signed-in user.");
        if (approver == entry.ConsultantUserId) throw new ConflictException("You cannot approve your own time. Ask another approver.");

        entry.IsApproved = true;
        entry.ApprovedByUserId = approver;
        entry.ApprovedAt = ERP.Domain.Common.AppClock.UtcNow;

        await _timeRepo.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    private static void EnsureTakesTime(ServiceEngagement engagement)
    {
        if (engagement.Status == EngagementStatus.Cancelled)
            throw new ConflictException($"Engagement {engagement.EngagementCode} is cancelled and takes no more time.");
    }

    private async Task<TimeEntryRecord> LoadUnapprovedAsync(Guid id, string action, CancellationToken ct)
    {
        var entry = await _timeRepo.Tracked().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Time entry {id} not found.");
        if (entry.IsApproved) throw new ConflictException($"Approved time cannot be {action}.");
        return entry;
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
            t.CreatedAt,
            t.InvoiceId);
}
