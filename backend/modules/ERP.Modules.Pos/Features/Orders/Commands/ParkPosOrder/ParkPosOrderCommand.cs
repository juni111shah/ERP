using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Puts a ticket aside without taking money: no number from the sales sequence, no ledger entry, no stock movement.</summary>
public record ParkPosOrderCommand(
    Guid SessionId,
    Guid? CustomerPartyId,
    string? CustomerName,
    List<PosOrderLineInput> Lines,
    string? Notes = null) : ICommand<PosOrderDto>;
