using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>What the server would charge for these lines right now, without saving anything. The till needs this to show the amount to collect once price lists or a code apply.</summary>
public record QuotePosOrderQuery(
    Guid LegalEntityId,
    Guid? CustomerPartyId,
    List<PosOrderLineInput> Lines,
    string? PromoCode = null) : IQuery<PosOrderQuoteDto>;
