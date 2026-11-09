using System.Text.Json.Serialization;
using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Rings up a counter sale: prices it on the server, takes the tenders, posts it and issues the stock.</summary>
[method: JsonConstructor]
public record CreatePosOrderCommand(
    Guid LegalEntityId,
    Guid RegisterId,
    Guid SessionId,
    Guid? CustomerPartyId,
    string? CustomerName,
    List<PosOrderLineInput> Lines,
    List<PosPaymentInput> Payments,
    string? Notes = null,
    string? CustomFields = null,
    Guid? CashierUserId = null,
    string? CashierName = null,
    Guid? ParkedOrderId = null,
    string? PromoCode = null) : ICommand<PosOrderDto>
{
    // Backwards-compatible constructor for existing tests/callers specifying CashierUserId and CashierName
    public CreatePosOrderCommand(
        Guid LegalEntityId,
        Guid RegisterId,
        Guid SessionId,
        Guid? CustomerPartyId,
        string? CustomerName,
        Guid? CashierUserId,
        string? CashierName,
        List<PosOrderLineInput> Lines,
        List<PosPaymentInput> Payments,
        string? Notes,
        string? CustomFields)
        : this(LegalEntityId, RegisterId, SessionId, CustomerPartyId, CustomerName, Lines, Payments, Notes, CustomFields, CashierUserId, CashierName)
    {
    }
}
