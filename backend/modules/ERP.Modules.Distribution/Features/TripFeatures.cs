using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Domain.Catalog;
using ERP.Modules.Distribution.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Features;

public record TripInventoryItemDto(
    Guid Id,
    Guid TripId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Uom,
    decimal LoadedQuantity,
    decimal SoldQuantity,
    decimal ReturnedQuantity,
    decimal DamagedQuantity,
    decimal UnitPrice);

public record TripInventoryInput(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Uom,
    decimal LoadedQuantity,
    decimal UnitPrice);

public record VanSalesTripDto(
    Guid Id,
    Guid LegalEntityId,
    string TripNumber,
    Guid RouteId,
    string RouteName,
    Guid DriverUserId,
    string DriverName,
    string VehicleNumber,
    Guid WarehouseId,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    TripStatus Status,
    decimal TotalSalesAmount,
    decimal TotalCollectionsAmount,
    string? Notes,
    List<TripInventoryItemDto> InventoryItems,
    string? CustomFields,
    DateTimeOffset CreatedAt,
    Guid? InvoiceId = null,
    string? InvoiceNumber = null,
    IReadOnlyList<string>? Warnings = null);

public record VanSalesTripSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string TripNumber,
    Guid RouteId,
    string RouteName,
    string DriverName,
    string VehicleNumber,
    DateOnly DepartureDate,
    TripStatus Status,
    decimal TotalSalesAmount,
    decimal TotalCollectionsAmount,
    int ItemCount,
    DateTimeOffset CreatedAt);

public record CreateTripCommand(
    Guid LegalEntityId,
    Guid RouteId,
    Guid DriverUserId,
    string DriverName,
    string VehicleNumber,
    Guid WarehouseId,
    DateOnly DepartureDate,
    string? Notes,
    List<TripInventoryInput> Items,
    string? CustomFields) : ICommand<VanSalesTripDto>;

/// <param name="CustomerPartyId">When set, what was sold is invoiced to this customer at the trip's prices and the invoice is issued, posting revenue and the receivable.</param>
/// <param name="AllowUnaccounted">Every loaded unit must be sold, returned or damaged unless this is set.</param>
public record CompleteTripCommand(
    Guid TripId,
    decimal TotalSalesAmount,
    decimal TotalCollectionsAmount,
    List<RecordTripItemReconciliationInput> Items,
    string? Notes,
    Guid? CustomerPartyId = null,
    bool AllowUnaccounted = false) : ICommand<VanSalesTripDto>;

public record RecordTripItemReconciliationInput(
    Guid ItemId,
    decimal SoldQuantity,
    decimal ReturnedQuantity,
    decimal DamagedQuantity);

public record GetTripByIdQuery(Guid Id) : IQuery<VanSalesTripDto?>;

public class CreateTripValidator : AbstractValidator<CreateTripCommand>
{
    public CreateTripValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one inventory item must be loaded for van sales.");
        RuleFor(x => x.DriverName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VehicleNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Items).Must(i => i is null || i.Select(x => x.ItemId).Distinct().Count() == i.Count).WithMessage("An item can be loaded only once.");
        RuleForEach(x => x.Items).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.LoadedQuantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public class CompleteTripValidator : AbstractValidator<CompleteTripCommand>
{
    public CompleteTripValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.TotalSalesAmount).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x.TotalCollectionsAmount).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x.Items).NotNull();
        RuleFor(x => x.Items).Must(i => i is null || i.Select(x => x.ItemId).Distinct().Count() == i.Count).WithMessage("An item can be reconciled only once.");
        RuleForEach(x => x.Items).ChildRules(l =>
        {
            l.RuleFor(x => x.SoldQuantity).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.ReturnedQuantity).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.DamagedQuantity).GreaterThanOrEqualTo(0);
        });
    }
}

public class TripHandlers :
    IRequestHandler<CreateTripCommand, VanSalesTripDto>,
    IRequestHandler<CompleteTripCommand, VanSalesTripDto>,
    IRequestHandler<GetTripByIdQuery, VanSalesTripDto?>
{
    private readonly IRepository<VanSalesTrip> _trips;
    private readonly IRepository<DistributionRoute> _routes;
    private readonly INumberingService _numbering;
    private readonly IStockPostingService _stockPosting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILegalEntityGuard _guard;
    private readonly IReadRepository<Item> _catalog;
    private readonly IInvoicingService _invoicing;
    private readonly ICreditLimitService _creditLimits;

    public TripHandlers(
        IRepository<VanSalesTrip> trips,
        IRepository<DistributionRoute> routes,
        INumberingService numbering,
        IStockPostingService stockPosting,
        IUnitOfWork unitOfWork,
        ILegalEntityGuard guard,
        IReadRepository<Item> catalog,
        IInvoicingService invoicing,
        ICreditLimitService creditLimits)
    {
        _invoicing = invoicing;
        _creditLimits = creditLimits;
        _guard = guard;
        _catalog = catalog;
        _unitOfWork = unitOfWork;
        _trips = trips;
        _routes = routes;
        _numbering = numbering;
        _stockPosting = stockPosting;
    }

    public async Task<VanSalesTripDto> Handle(CreateTripCommand request, CancellationToken cancellationToken)
    {
        await _guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var route = await _routes.FindAsync(request.RouteId, cancellationToken);
        if (route is null || route.LegalEntityId != request.LegalEntityId) throw new FieldValidationException("routeId", "Route not found in this legal entity.");
        if (!await _stockPosting.IsWarehouseUsableAsync(request.WarehouseId, request.LegalEntityId, cancellationToken)) throw new FieldValidationException("warehouseId", "The warehouse was not found, is not active, or belongs to another legal entity.");
        var routeName = route.Name;

        var itemIds = request.Items.Select(i => i.ItemId).ToList();
        var catalog = await _catalog.Query(new Specification<Item>(i => itemIds.Contains(i.Id))).Select(i => new { i.Id, i.Sku, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        if (catalog.Count != itemIds.Distinct().Count()) throw new FieldValidationException("items", "An item was not found.");

        var tripNumber = await _numbering.NextAsync("VanSalesTrip", request.LegalEntityId, request.DepartureDate, cancellationToken);

        var trip = new VanSalesTrip
        {
            LegalEntityId = request.LegalEntityId,
            TripNumber = tripNumber,
            RouteId = request.RouteId,
            RouteName = routeName,
            DriverUserId = request.DriverUserId,
            DriverName = request.DriverName.Trim(),
            VehicleNumber = request.VehicleNumber.Trim(),
            WarehouseId = request.WarehouseId,
            DepartureDate = request.DepartureDate,
            Status = TripStatus.InProgress,
            TotalSalesAmount = 0,
            TotalCollectionsAmount = 0,
            Notes = request.Notes?.Trim(),
            CustomFields = request.CustomFields
        };

        foreach (var itm in request.Items)
        {
            trip.InventoryItems.Add(new TripInventoryItem
            {
                LegalEntityId = trip.LegalEntityId,
                TripId = trip.Id,
                ItemId = itm.ItemId,
                ItemCode = catalog[itm.ItemId].Sku, // from the catalog, not from the request
                ItemName = catalog[itm.ItemId].Name,
                Uom = string.IsNullOrWhiteSpace(itm.Uom) ? "PCS" : itm.Uom.Trim(),
                LoadedQuantity = itm.LoadedQuantity,
                SoldQuantity = 0,
                ReturnedQuantity = 0,
                DamagedQuantity = 0,
                UnitPrice = itm.UnitPrice
            });
        }

        _trips.Add(trip);
        await _trips.SaveChangesAsync(cancellationToken);
        return Map(trip);
    }

    // Trip status and the stock issues for what was sold or damaged commit together.
    public Task<VanSalesTripDto> Handle(CompleteTripCommand request, CancellationToken cancellationToken)
        => _unitOfWork.InTransactionAsync(token => CompleteAsync(request, token), cancellationToken);

    private async Task<VanSalesTripDto> CompleteAsync(CompleteTripCommand request, CancellationToken cancellationToken)
    {
        var trip = await _trips.Tracked(new Specification<VanSalesTrip>(t => t.Id == request.TripId))
            .Include(t => t.InventoryItems)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        if (trip.Status == TripStatus.Completed)
        {
            throw new ConflictException("Trip is already completed.");
        }
        if (trip.Status == TripStatus.Cancelled) throw new ConflictException("A cancelled trip cannot be completed.");

        foreach (var rec in request.Items)
        {
            var loaded = trip.InventoryItems.FirstOrDefault(i => i.ItemId == rec.ItemId)
                ?? throw new FieldValidationException("items", "An item in the reconciliation was not loaded on this trip.");
            if (rec.SoldQuantity + rec.ReturnedQuantity + rec.DamagedQuantity > loaded.LoadedQuantity)
                throw new FieldValidationException("items", $"{loaded.ItemCode}: sold, returned and damaged exceed the {loaded.LoadedQuantity} loaded.");
        }
        if (!request.AllowUnaccounted)
        {
            var missing = trip.InventoryItems.FirstOrDefault(i =>
                request.Items.Where(r => r.ItemId == i.ItemId).Sum(r => r.SoldQuantity + r.ReturnedQuantity + r.DamagedQuantity) < i.LoadedQuantity);
            if (missing is not null)
                throw new FieldValidationException("items", $"{missing.ItemCode}: {missing.LoadedQuantity} was loaded but not all of it is sold, returned or damaged. Account for every unit, or set allowUnaccounted.");
        }

        trip.Status = TripStatus.Completed;
        trip.ReturnDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        trip.TotalSalesAmount = request.TotalSalesAmount;
        trip.TotalCollectionsAmount = request.TotalCollectionsAmount;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            trip.Notes = (trip.Notes + " | Reconciled: " + request.Notes).Trim();
        }

        foreach (var rec in request.Items)
        {
            var line = trip.InventoryItems.FirstOrDefault(i => i.ItemId == rec.ItemId);
            if (line != null)
            {
                line.SoldQuantity = rec.SoldQuantity;
                line.ReturnedQuantity = rec.ReturnedQuantity;
                line.DamagedQuantity = rec.DamagedQuantity;
            }
        }

        await _trips.SaveChangesAsync(cancellationToken);

        foreach (var item in trip.InventoryItems)
        {
            var issuedQty = item.SoldQuantity + item.DamagedQuantity;
            if (issuedQty > 0 && item.ItemId != Guid.Empty)
            {
                await _stockPosting.IssueStockAsync(new StockIssueRequest(
                    trip.LegalEntityId,
                    item.ItemId,
                    issuedQty,
                    trip.ReturnDate ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
                    trip.TripNumber,
                    $"Van sales trip {trip.TripNumber} sold/damaged ({item.ItemCode})",
                    trip.WarehouseId != Guid.Empty ? trip.WarehouseId : null,
                    EnforceAvailable: true), cancellationToken);
            }
        }

        var warning = request.CustomerPartyId is { } customer ? await InvoiceSoldGoodsAsync(trip, customer, cancellationToken) : null;

        return Map(trip) with { Warnings = warning is null ? null : [warning] };
    }

    /// <summary>One invoice per trip for what it sold: the stock issue above already posted the cost, this posts the revenue and the receivable.</summary>
    private async Task<string?> InvoiceSoldGoodsAsync(VanSalesTrip trip, Guid customer, CancellationToken ct)
    {
        var sold = trip.InventoryItems.Where(i => i.SoldQuantity > 0).ToList();
        if (sold.Count == 0) return null;

        var itemIds = sold.Select(i => i.ItemId).ToList();
        var taxCodes = await _catalog.Query(new Specification<Item>(i => itemIds.Contains(i.Id))).Select(i => new { i.Id, i.TaxCodeId }).ToDictionaryAsync(i => i.Id, i => i.TaxCodeId, ct);
        var date = trip.ReturnDate ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var invoice = await _invoicing.BuildAsync(new InvoiceDraft(trip.LegalEntityId, customer, date, null,
            sold.Select(i => new InvoiceLineDraft($"{trip.TripNumber}: {i.ItemName}", i.SoldQuantity, i.UnitPrice, taxCodes.GetValueOrDefault(i.ItemId), ItemId: i.ItemId)).ToList(),
            SourceType: "vansalestrip", SourceId: trip.Id, Notes: $"Van sales trip {trip.TripNumber}"), ct);
        if (invoice.Total <= 0) throw new FieldValidationException("items", "What this trip sold has no value to invoice: set a unit price on the sold items.");

        var warning = await _creditLimits.CheckAsync(customer, trip.LegalEntityId, invoice.Total, ct);
        await _invoicing.IssueAsync(invoice, ct);
        trip.InvoiceId = invoice.Id;
        trip.InvoiceNumber = invoice.Number;
        trip.TotalSalesAmount = invoice.Total;
        await _trips.SaveChangesAsync(ct);
        return warning;
    }

    public async Task<VanSalesTripDto?> Handle(GetTripByIdQuery request, CancellationToken cancellationToken)
    {
        var trip = await _trips.Query(new Specification<VanSalesTrip>(t => t.Id == request.Id))
            .Include(t => t.InventoryItems)
            .FirstOrDefaultAsync(cancellationToken);

        return trip is null ? null : Map(trip);
    }

    private static VanSalesTripDto Map(VanSalesTrip t) => new(
        t.Id,
        t.LegalEntityId,
        t.TripNumber,
        t.RouteId,
        t.RouteName,
        t.DriverUserId,
        t.DriverName,
        t.VehicleNumber,
        t.WarehouseId,
        t.DepartureDate,
        t.ReturnDate,
        t.Status,
        t.TotalSalesAmount,
        t.TotalCollectionsAmount,
        t.Notes,
        t.InventoryItems.Select(i => new TripInventoryItemDto(
            i.Id,
            i.TripId,
            i.ItemId,
            i.ItemCode,
            i.ItemName,
            i.Uom,
            i.LoadedQuantity,
            i.SoldQuantity,
            i.ReturnedQuantity,
            i.DamagedQuantity,
            i.UnitPrice)).ToList(),
        t.CustomFields,
        t.CreatedAt,
        t.InvoiceId,
        t.InvoiceNumber);
}
