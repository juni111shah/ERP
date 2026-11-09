namespace ERP.Modules.Distribution.Features;

public record DeliveryStopInput(
    Guid CustomerPartyId,
    string CustomerName,
    int SequenceOrder,
    string? Address,
    string? ContactPhone,
    string? PreferredTimeWindow,
    string? SpecialInstructions);
