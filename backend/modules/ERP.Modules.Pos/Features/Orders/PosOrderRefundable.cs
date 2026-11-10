using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

/// <summary>A sale as the counter reads it, with what is left to refund. Used by the look-ups by id and by receipt number.</summary>
public sealed class PosOrderRefundable(IPosReturnRepository returns)
{
    /// <summary>What can still go back in money and what is still on account, so a return can be split the way the sale was paid.</summary>
    public async Task<PosOrderDto> WithRefundableAsync(PosOrder order, CancellationToken ct)
    {
        var prior = await returns.ListRefundsOfOrderAsync(order.Id, ct);
        var paid = order.Payments.Where(p => p.Method != PosPaymentMethod.Account).Sum(p => p.Amount);
        return PosOrderDto.From(order) with
        {
            MoneyLeftToRefund = paid - prior.Where(f => f.Method != PosPaymentMethod.Account).Sum(f => f.Amount),
            OnAccountLeft = order.TotalAmount - paid - prior.Where(f => f.Method == PosPaymentMethod.Account).Sum(f => f.Amount),
        };
    }
}
