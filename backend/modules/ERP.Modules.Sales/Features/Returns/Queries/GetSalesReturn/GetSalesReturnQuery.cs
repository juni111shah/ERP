using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>One sales return with its lines.</summary>
public sealed record GetSalesReturnQuery(Guid Id) : IQuery<SalesReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsRead; }
