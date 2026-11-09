using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class CompleteTripHandler(
    IVanSalesTripRepository trips,
    IItemRepository items,
    IStockPostingService stockPosting,
    IInvoicingService invoicing,
    ICreditLimitService creditLimits,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CompleteTripCommand, VanSalesTripDto>
{
    // Trip status and the stock issues for what was sold or damaged commit together.
    public Task<VanSalesTripDto> Handle(CompleteTripCommand request, CancellationToken cancellationToken)
        => unitOfWork.InTransactionAsync(token => CompleteAsync(request, token), cancellationToken);

    private async Task<VanSalesTripDto> CompleteAsync(CompleteTripCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it; only a trip in progress completes.
        var trip = await trips.GetWithItemsForUpdateAsync(request.TripId, cancellationToken);

        if (trip.Status == TripStatus.Completed)
        {
            throw new ConflictException("Trip is already completed.");
        }
        if (trip.Status == TripStatus.Cancelled) throw new ConflictException("A cancelled trip cannot be completed.");

        // 2. What is reconciled must have been loaded, and every loaded unit accounted for unless allowed.
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

        // 3. Complete the trip and record the reconciliation.
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

        await trips.SaveChangesAsync(cancellationToken);

        // 4. Issue from stock what was sold or damaged.
        foreach (var item in trip.InventoryItems)
        {
            var issuedQty = item.SoldQuantity + item.DamagedQuantity;
            if (issuedQty > 0 && item.ItemId != Guid.Empty)
            {
                await stockPosting.IssueStockAsync(new StockIssueRequest(
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

        // 5. Invoice the sales to the customer, when one was given.
        var warning = request.CustomerPartyId is { } customer ? await InvoiceSoldGoodsAsync(trip, customer, cancellationToken) : null;

        return VanSalesTripDto.From(trip) with { Warnings = warning is null ? null : [warning] };
    }

    /// <summary>One invoice per trip for what it sold: the stock issue above already posted the cost, this posts the revenue and the receivable.</summary>
    private async Task<string?> InvoiceSoldGoodsAsync(VanSalesTrip trip, Guid customer, CancellationToken ct)
    {
        var sold = trip.InventoryItems.Where(i => i.SoldQuantity > 0).ToList();
        if (sold.Count == 0) return null;

        var itemIds = sold.Select(i => i.ItemId).ToList();
        var taxCodes = await items.GetTaxCodesAsync(itemIds, ct);
        var date = trip.ReturnDate ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var invoice = await invoicing.BuildAsync(new InvoiceDraft(trip.LegalEntityId, customer, date, null,
            sold.Select(i => new InvoiceLineDraft($"{trip.TripNumber}: {i.ItemName}", i.SoldQuantity, i.UnitPrice, taxCodes.GetValueOrDefault(i.ItemId), ItemId: i.ItemId)).ToList(),
            SourceType: "vansalestrip", SourceId: trip.Id, Notes: $"Van sales trip {trip.TripNumber}"), ct);
        if (invoice.Total <= 0) throw new FieldValidationException("items", "What this trip sold has no value to invoice: set a unit price on the sold items.");

        var warning = await creditLimits.CheckAsync(customer, trip.LegalEntityId, invoice.Total, ct);
        await invoicing.IssueAsync(invoice, ct);
        trip.InvoiceId = invoice.Id;
        trip.InvoiceNumber = invoice.Number;
        trip.TotalSalesAmount = invoice.Total;
        await trips.SaveChangesAsync(ct);
        return warning;
    }
}
