using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>One wholesale price tier; null when it does not exist.</summary>
public record GetWholesalePriceByIdQuery(Guid Id) : IQuery<WholesalePriceMatrixDto?>;
