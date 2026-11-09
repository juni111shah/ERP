using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for the cheque register (received and issued cheques). Implemented in ERP.Infrastructure/Repositories/Finance/PaymentInstrumentRepository.cs.</summary>
public interface IPaymentInstrumentRepository : IRepository<PaymentInstrument>
{
    /// <summary>The cheque, tracked for changes; null when not found.</summary>
    Task<PaymentInstrument?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>A cheque we issued to a vendor, tracked for changes. Throws NotFoundException.</summary>
    Task<PaymentInstrument> GetIssuedForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The cheque details shown on a receipt or payment; null when there is no instrument.</summary>
    Task<InstrumentInfo?> GetInfoAsync(Guid? id, CancellationToken ct);

    /// <summary>Status, amount and date of every cheque received (for the register summary).</summary>
    Task<List<ChequeSummaryItem>> ListReceivedForSummaryAsync(CancellationToken ct);

    /// <summary>The cheque that replaced <paramref name="id"/>, if any.</summary>
    Task<Guid?> GetReplacementIdAsync(Guid id, CancellationToken ct);
}
