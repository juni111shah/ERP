using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class ParkPosOrderHandler(IPosOrderRepository orders, IPosSessionRepository sessions, PosOrderPricing pricing, PosOrderParties people)
    : ICommandHandler<ParkPosOrderCommand, PosOrderDto>
{
    public async Task<PosOrderDto> Handle(ParkPosOrderCommand request, CancellationToken cancellationToken)
    {
        // 1. A ticket is held in an open shift.
        var session = await sessions.FindAsync(request.SessionId, cancellationToken)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");
        if (session.Status != PosSessionStatus.Open) throw new ConflictException("Open a shift to hold a sale.");

        // 2. Price it as a sale would be priced.
        var (cashierName, cashierId) = await people.CashierAsync(null, null, cancellationToken);
        var (customerId, customerName) = await people.CustomerAsync(request.CustomerPartyId, request.CustomerName, cancellationToken);
        var priced = await pricing.PriceAsync(session.LegalEntityId, customerId, request.Lines, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        // 3. Save it on hold, under a hold number.
        var order = new PosOrder
        {
            Number = $"HOLD-{now:yyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            LegalEntityId = session.LegalEntityId,
            RegisterId = session.RegisterId,
            SessionId = session.Id,
            CustomerPartyId = customerId,
            CustomerName = customerName,
            OrderDate = now,
            SubTotal = priced.SubTotal,
            TaxTotal = priced.TaxTotal,
            DiscountTotal = priced.DiscountTotal,
            TotalAmount = priced.Total,
            Status = PosOrderStatus.Parked,
            PaymentStatus = PosPaymentStatus.Unpaid,
            CashierUserId = cashierId,
            CashierName = cashierName,
            Notes = request.Notes,
            Lines = priced.Lines,
        };
        orders.Add(order);
        await orders.SaveChangesAsync(cancellationToken);
        return PosOrderDto.From(order);
    }
}
