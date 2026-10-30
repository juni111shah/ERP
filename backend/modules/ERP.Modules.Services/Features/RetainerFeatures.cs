using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Services.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Features;

public record RetainerDto(
    Guid Id,
    Guid LegalEntityId,
    string RetainerNumber,
    Guid EngagementId,
    Guid ClientId,
    string ClientName,
    string Title,
    RetainerCycle Cycle,
    RetainerStatus Status,
    decimal RecurringFee,
    decimal IncludedHours,
    decimal OverageHourlyRate,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Terms,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record RetainerSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string RetainerNumber,
    Guid EngagementId,
    string ClientName,
    string Title,
    RetainerCycle Cycle,
    RetainerStatus Status,
    decimal RecurringFee,
    decimal IncludedHours,
    DateOnly StartDate,
    DateTimeOffset CreatedAt);

public record CreateRetainerCommand(
    Guid LegalEntityId,
    Guid EngagementId,
    Guid ClientId,
    string ClientName,
    string Title,
    RetainerCycle Cycle,
    decimal RecurringFee,
    decimal IncludedHours,
    decimal OverageHourlyRate,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Terms,
    string? CustomFields) : ICommand<RetainerDto>;

public record UpdateRetainerStatusCommand(
    Guid Id,
    RetainerStatus Status) : ICommand<RetainerDto>;

public record GetRetainerByIdQuery(Guid Id) : IQuery<RetainerDto?>;

public class CreateRetainerValidator : AbstractValidator<CreateRetainerCommand>
{
    public CreateRetainerValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.EngagementId).NotEmpty().WithMessage("Engagement is required.");
        RuleFor(x => x.ClientId).NotEmpty().WithMessage("Client is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RecurringFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IncludedHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OverageHourlyRate).GreaterThanOrEqualTo(0);
    }
}

public class UpdateRetainerStatusValidator : AbstractValidator<UpdateRetainerStatusCommand>
{
    public UpdateRetainerStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class RetainerHandlers :
    IRequestHandler<CreateRetainerCommand, RetainerDto>,
    IRequestHandler<UpdateRetainerStatusCommand, RetainerDto>,
    IRequestHandler<GetRetainerByIdQuery, RetainerDto?>
{
    private readonly IRepository<RetainerContract> _repo;
    private readonly INumberingService _numbering;

    public RetainerHandlers(IRepository<RetainerContract> repo, INumberingService numbering)
    {
        _repo = repo;
        _numbering = numbering;
    }

    public async Task<RetainerDto> Handle(CreateRetainerCommand request, CancellationToken cancellationToken)
    {
        var number = await _numbering.NextAsync("RET", request.LegalEntityId, request.StartDate, cancellationToken);
        var contract = new RetainerContract
        {
            LegalEntityId = request.LegalEntityId,
            RetainerNumber = number,
            EngagementId = request.EngagementId,
            ClientId = request.ClientId,
            ClientName = request.ClientName.Trim(),
            Title = request.Title.Trim(),
            Cycle = request.Cycle,
            Status = RetainerStatus.Active,
            RecurringFee = request.RecurringFee,
            IncludedHours = request.IncludedHours,
            OverageHourlyRate = request.OverageHourlyRate,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Terms = request.Terms?.Trim(),
            CustomFields = request.CustomFields
        };

        _repo.Add(contract);
        await _repo.SaveChangesAsync(cancellationToken);

        return Map(contract);
    }

    public async Task<RetainerDto> Handle(UpdateRetainerStatusCommand request, CancellationToken cancellationToken)
    {
        var contract = await _repo.Tracked().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Retainer contract {request.Id} not found.");

        ServicesTransitions.Retainer.Ensure(contract.Status, request.Status, isSystem: false);

        contract.Status = request.Status;
        await _repo.SaveChangesAsync(cancellationToken);
        return Map(contract);
    }

    public async Task<RetainerDto?> Handle(GetRetainerByIdQuery request, CancellationToken cancellationToken)
    {
        var contract = await _repo.FindAsync(request.Id, cancellationToken);
        return contract is null ? null : Map(contract);
    }

    private static RetainerDto Map(RetainerContract r) =>
        new(
            r.Id,
            r.LegalEntityId,
            r.RetainerNumber,
            r.EngagementId,
            r.ClientId,
            r.ClientName,
            r.Title,
            r.Cycle,
            r.Status,
            r.RecurringFee,
            r.IncludedHours,
            r.OverageHourlyRate,
            r.StartDate,
            r.EndDate,
            r.Terms,
            r.CustomFields,
            r.CreatedAt);
}
