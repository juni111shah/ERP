using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>A page of the till's catalogue with prices, tax and stock. Code is an exact look-up (a scan: barcode, then SKU). Search matches part of the name, SKU or barcode.</summary>
public record GetPosCatalogQuery(Guid? RegisterId, string? Search, string? Code, int Page = 1, int PageSize = 60) : IQuery<PosCatalogPageDto>;
