using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>One retail store; null when it does not exist.</summary>
public record GetStoreByIdQuery(Guid Id) : IQuery<RetailStoreDto?>;
