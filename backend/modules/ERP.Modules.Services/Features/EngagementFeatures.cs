using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Services.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Features;

public record EngagementDto(
    Guid Id,
    Guid LegalEntityId,
    string EngagementCode,
    string Title,
    Guid ClientId,
    string ClientName,
    EngagementType Type,
    EngagementStatus Status,
    decimal ContractValue,
    decimal BudgetedHours,
    decimal ConsumedHours,
    decimal ConsumedCost,
    DateOnly StartDate,
    DateOnly? TargetEndDate,
    DateOnly? ActualEndDate,
    Guid? LeadConsultantUserId,
    string? LeadConsultantName,
    string? Description,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record EngagementSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string EngagementCode,
    string Title,
    Guid ClientId,
    string ClientName,
    EngagementType Type,
    EngagementStatus Status,
    decimal ContractValue,
    decimal BudgetedHours,
    decimal ConsumedHours,
    DateOnly StartDate,
    DateOnly? TargetEndDate,
    string? LeadConsultantName,
    DateTimeOffset CreatedAt);

public record CreateEngagementCommand(
    Guid LegalEntityId,
    string Title,
    Guid ClientId,
    string ClientName,
    EngagementType Type,
    decimal ContractValue,
    decimal BudgetedHours,
    DateOnly StartDate,
    DateOnly? TargetEndDate,
    Guid? LeadConsultantUserId,
    string? LeadConsultantName,
    string? Description,
    string? CustomFields) : ICommand<EngagementDto>;

public record UpdateEngagementStatusCommand(
    Guid Id,
    EngagementStatus Status) : ICommand<EngagementDto>;

public record GetEngagementByIdQuery(Guid Id) : IQuery<EngagementDto?>;

public class CreateEngagementValidator : AbstractValidator<CreateEngagementCommand>
{
    public CreateEngagementValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("Title is required.");
        RuleFor(x => x.ClientId).NotEmpty().WithMessage("Client is required.");
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BudgetedHours).GreaterThanOrEqualTo(0);
    }
}

public class UpdateEngagementStatusValidator : AbstractValidator<UpdateEngagementStatusCommand>
{
    public UpdateEngagementStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class EngagementHandlers :
    IRequestHandler<CreateEngagementCommand, EngagementDto>,
    IRequestHandler<UpdateEngagementStatusCommand, EngagementDto>,
    IRequestHandler<GetEngagementByIdQuery, EngagementDto?>
{
    private readonly IRepository<ServiceEngagement> _repo;
    private readonly INumberingService _numbering;

    public EngagementHandlers(IRepository<ServiceEngagement> repo, INumberingService numbering)
    {
        _repo = repo;
        _numbering = numbering;
    }

    public async Task<EngagementDto> Handle(CreateEngagementCommand request, CancellationToken cancellationToken)
    {
        var code = await _numbering.NextAsync("ENG", request.LegalEntityId, request.StartDate, cancellationToken);
        var engagement = new ServiceEngagement
        {
            LegalEntityId = request.LegalEntityId,
            EngagementCode = code,
            Title = request.Title.Trim(),
            ClientId = request.ClientId,
            ClientName = request.ClientName.Trim(),
            Type = request.Type,
            Status = EngagementStatus.Active,
            ContractValue = request.ContractValue,
            BudgetedHours = request.BudgetedHours,
            ConsumedHours = 0,
            ConsumedCost = 0,
            StartDate = request.StartDate,
            TargetEndDate = request.TargetEndDate,
            LeadConsultantUserId = request.LeadConsultantUserId,
            LeadConsultantName = request.LeadConsultantName?.Trim(),
            Description = request.Description?.Trim(),
            CustomFields = request.CustomFields
        };

        _repo.Add(engagement);
        await _repo.SaveChangesAsync(cancellationToken);

        return Map(engagement);
    }

    public async Task<EngagementDto> Handle(UpdateEngagementStatusCommand request, CancellationToken cancellationToken)
    {
        var engagement = await _repo.FindAsync(request.Id, cancellationToken);
        if (engagement is null)
            throw new KeyNotFoundException($"Service engagement {request.Id} not found.");

        engagement.Status = request.Status;
        if (request.Status == EngagementStatus.Completed && engagement.ActualEndDate is null)
        {
            engagement.ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        await _repo.SaveChangesAsync(cancellationToken);
        return Map(engagement);
    }

    public async Task<EngagementDto?> Handle(GetEngagementByIdQuery request, CancellationToken cancellationToken)
    {
        var engagement = await _repo.FindAsync(request.Id, cancellationToken);
        return engagement is null ? null : Map(engagement);
    }

    private static EngagementDto Map(ServiceEngagement e) =>
        new(
            e.Id,
            e.LegalEntityId,
            e.EngagementCode,
            e.Title,
            e.ClientId,
            e.ClientName,
            e.Type,
            e.Status,
            e.ContractValue,
            e.BudgetedHours,
            e.ConsumedHours,
            e.ConsumedCost,
            e.StartDate,
            e.TargetEndDate,
            e.ActualEndDate,
            e.LeadConsultantUserId,
            e.LeadConsultantName,
            e.Description,
            e.CustomFields,
            e.CreatedAt);
}
