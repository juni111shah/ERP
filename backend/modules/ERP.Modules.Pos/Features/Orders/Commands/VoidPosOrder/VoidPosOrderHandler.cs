using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class VoidPosOrderHandler(
    IPosOrderRepository orders,
    IPosSessionRepository sessions,
    IPosInvoiceRepository invoices,
    IPosJournalRepository journals,
    IStockPostingService stockPosting,
    IPostingService posting,
    IInvoicingService invoicing,
    IUnitOfWork unitOfWork,
    PosOrderPricing pricing) : ICommandHandler<VoidPosOrderCommand, PosOrderDto>
{
    public async Task<PosOrderDto> Handle(VoidPosOrderCommand request, CancellationToken cancellationToken)
    {
        return await unitOfWork.InTransactionAsync(async token =>
        {
            // 1. Only a completed sale with nothing returned, in an open shift, can be voided.
            var order = await orders.FindWithLinesAndPaymentsForUpdateAsync(request.OrderId, token)
                ?? throw new NotFoundException($"Order {request.OrderId} not found");

            if (order.Status == PosOrderStatus.Voided)
            {
                throw new ConflictException("Order is already voided.");
            }
            if (order.Status != PosOrderStatus.Completed)
            {
                throw new ConflictException("Only a completed order can be voided.");
            }
            if (order.ReturnedTotal > 0)
            {
                throw new ConflictException("Part of this sale has been returned, so it can no longer be voided. Return the rest instead.");
            }

            var session = await sessions.FindAsync(order.SessionId, token)
                ?? throw new NotFoundException($"Session {order.SessionId} not found");

            if (session.Status != PosSessionStatus.Open)
            {
                throw new ConflictException("Cannot void orders in a closed session.");
            }

            // 2. Take the sale back out of the shift.
            var cashRefunded = order.Payments.Where(p => p.Method == PosPaymentMethod.Cash).Sum(p => p.Amount);

            var updatedSession = await sessions.VoidSaleAsync(order.SessionId, order.TotalAmount, cashRefunded, token);

            if (updatedSession == 0)
            {
                throw new ConflictException("Cannot void orders in a closed session.");
            }

            // 3. Void the order and the invoice of what was on account.
            order.Status = PosOrderStatus.Voided;
            order.PaymentStatus = PosPaymentStatus.Refunded;
            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? $"Voided: {request.Reason}"
                : $"{order.Notes} | Voided: {request.Reason}";

            if (order.InvoiceId is { } invoiceId)
            {
                var invoice = await invoices.FindForUpdateAsync(invoiceId, token)
                    ?? throw new NotFoundException($"Invoice {order.InvoiceNumber} not found");
                await invoicing.VoidAsync(invoice, $"Void POS order {order.Number}", token); // refuses once the customer has paid some of it
            }

            await orders.SaveChangesAsync(token);

            // 4. Give back the discount code use and the stock, and reverse the ledger entry.
            if (order.PromotionId is { } promotionId && pricing.PriceResolver is not null) await pricing.PriceResolver.ReleaseDiscountAsync(promotionId, token);

            await stockPosting.ReverseIssueAsync(order.Number, token);

            var journal = await journals.FindPostedForSourceForUpdateAsync(PosOrderJournal.Source, order.Id, token);
            if (journal is not null)
            {
                await posting.ReverseTodayAsync(journal, $"Void POS order {order.Number}", token);
                await orders.SaveChangesAsync(token);
            }

            return PosOrderDto.From(order);
        }, cancellationToken);
    }
}
