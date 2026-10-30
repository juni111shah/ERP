using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Distribution.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Features;

public record DeliveryStopDto(
    Guid Id,
    Guid RouteId,
    Guid CustomerPartyId,
    string CustomerName,
    int SequenceOrder,
    string? Address,
    string? ContactPhone,
    string? PreferredTimeWindow,
    string? SpecialInstructions,
    bool IsActive);

public record DeliveryStopInput(
    Guid CustomerPartyId,
    string CustomerName,
    int SequenceOrder,
    string? Address,
    string? ContactPhone,
    string? PreferredTimeWindow,
    string? SpecialInstructions);

public record DistributionRouteDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    string? Description,
    Guid? AssignedDriverUserId,
    string? DriverName,
    string? VehicleNumber,
    decimal? VehicleCapacityKg,
    Guid? WarehouseId,
    bool IsActive,
    int StopCount,
    List<DeliveryStopDto> Stops,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record DistributionRouteSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    string? DriverName,
    string? VehicleNumber,
    decimal? VehicleCapacityKg,
    Guid? WarehouseId,
    bool IsActive,
    int StopCount,
    DateTimeOffset CreatedAt);

public record CreateRouteCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    string? Description,
    Guid? AssignedDriverUserId,
    string? DriverName,
    string? VehicleNumber,
    decimal? VehicleCapacityKg,
    Guid? WarehouseId,
    bool IsActive,
    List<DeliveryStopInput>? Stops,
    string? CustomFields) : ICommand<DistributionRouteDto>;

public record UpdateRouteCommand(
    Guid Id,
    string Name,
    string? Description,
    Guid? AssignedDriverUserId,
    string? DriverName,
    string? VehicleNumber,
    decimal? VehicleCapacityKg,
    Guid? WarehouseId,
    bool IsActive,
    List<DeliveryStopInput>? Stops,
    string? CustomFields) : ICommand<DistributionRouteDto>;

public record DeleteRouteCommand(Guid Id) : ICommand<Unit>;

public record GetRouteByIdQuery(Guid Id) : IQuery<DistributionRouteDto?>;

public class CreateRouteValidator : AbstractValidator<CreateRouteCommand>
{
    public CreateRouteValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
    }
}

public class UpdateRouteValidator : AbstractValidator<UpdateRouteCommand>
{
    public UpdateRouteValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class RouteHandlers :
    IRequestHandler<CreateRouteCommand, DistributionRouteDto>,
    IRequestHandler<UpdateRouteCommand, DistributionRouteDto>,
    IRequestHandler<DeleteRouteCommand, Unit>,
    IRequestHandler<GetRouteByIdQuery, DistributionRouteDto?>
{
    private readonly IRepository<DistributionRoute> _routes;

    public RouteHandlers(IRepository<DistributionRoute> routes)
    {
        _routes = routes;
    }

    public async Task<DistributionRouteDto> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
    {
        var route = new DistributionRoute
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            AssignedDriverUserId = request.AssignedDriverUserId,
            DriverName = request.DriverName?.Trim(),
            VehicleNumber = request.VehicleNumber?.Trim(),
            VehicleCapacityKg = request.VehicleCapacityKg,
            WarehouseId = request.WarehouseId,
            IsActive = request.IsActive,
            CustomFields = request.CustomFields
        };

        if (request.Stops != null && request.Stops.Count > 0)
        {
            foreach (var s in request.Stops)
            {
                route.Stops.Add(new DeliveryStop
                {
                    LegalEntityId = route.LegalEntityId,
                    RouteId = route.Id,
                    CustomerPartyId = s.CustomerPartyId,
                    CustomerName = s.CustomerName.Trim(),
                    SequenceOrder = s.SequenceOrder,
                    Address = s.Address?.Trim(),
                    ContactPhone = s.ContactPhone?.Trim(),
                    PreferredTimeWindow = s.PreferredTimeWindow?.Trim(),
                    SpecialInstructions = s.SpecialInstructions?.Trim(),
                    IsActive = true
                });
            }
        }

        _routes.Add(route);
        await _routes.SaveChangesAsync(cancellationToken);
        return Map(route);
    }

    public async Task<DistributionRouteDto> Handle(UpdateRouteCommand request, CancellationToken cancellationToken)
    {
        var route = await _routes.Query(new Specification<DistributionRoute>(r => r.Id == request.Id))
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Route {request.Id} not found.");

        route.Name = request.Name.Trim();
        route.Description = request.Description?.Trim();
        route.AssignedDriverUserId = request.AssignedDriverUserId;
        route.DriverName = request.DriverName?.Trim();
        route.VehicleNumber = request.VehicleNumber?.Trim();
        route.VehicleCapacityKg = request.VehicleCapacityKg;
        route.WarehouseId = request.WarehouseId;
        route.IsActive = request.IsActive;
        route.CustomFields = request.CustomFields;

        if (request.Stops != null)
        {
            route.Stops.Clear();
            foreach (var s in request.Stops)
            {
                route.Stops.Add(new DeliveryStop
                {
                    LegalEntityId = route.LegalEntityId,
                    RouteId = route.Id,
                    CustomerPartyId = s.CustomerPartyId,
                    CustomerName = s.CustomerName.Trim(),
                    SequenceOrder = s.SequenceOrder,
                    Address = s.Address?.Trim(),
                    ContactPhone = s.ContactPhone?.Trim(),
                    PreferredTimeWindow = s.PreferredTimeWindow?.Trim(),
                    SpecialInstructions = s.SpecialInstructions?.Trim(),
                    IsActive = true
                });
            }
        }

        await _routes.SaveChangesAsync(cancellationToken);
        return Map(route);
    }

    public async Task<Unit> Handle(DeleteRouteCommand request, CancellationToken cancellationToken)
    {
        var route = await _routes.GetForUpdateAsync(request.Id, cancellationToken);
        _routes.Remove(route);
        await _routes.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<DistributionRouteDto?> Handle(GetRouteByIdQuery request, CancellationToken cancellationToken)
    {
        var route = await _routes.Query(new Specification<DistributionRoute>(r => r.Id == request.Id))
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(cancellationToken);

        return route is null ? null : Map(route);
    }

    private static DistributionRouteDto Map(DistributionRoute r) => new(
        r.Id,
        r.LegalEntityId,
        r.Code,
        r.Name,
        r.Description,
        r.AssignedDriverUserId,
        r.DriverName,
        r.VehicleNumber,
        r.VehicleCapacityKg,
        r.WarehouseId,
        r.IsActive,
        r.Stops.Count,
        r.Stops.OrderBy(s => s.SequenceOrder).Select(s => new DeliveryStopDto(
            s.Id,
            s.RouteId,
            s.CustomerPartyId,
            s.CustomerName,
            s.SequenceOrder,
            s.Address,
            s.ContactPhone,
            s.PreferredTimeWindow,
            s.SpecialInstructions,
            s.IsActive)).ToList(),
        r.CustomFields,
        r.CreatedAt);
}
