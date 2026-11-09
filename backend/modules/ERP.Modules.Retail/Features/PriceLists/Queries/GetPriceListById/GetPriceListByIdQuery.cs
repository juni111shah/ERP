using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>A price list with its rows.</summary>
public record GetPriceListByIdQuery(Guid Id) : IQuery<RetailPriceListDto>;
