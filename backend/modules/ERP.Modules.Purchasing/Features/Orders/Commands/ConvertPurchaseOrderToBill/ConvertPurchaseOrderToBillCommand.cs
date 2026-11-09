using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>
/// Raises one draft bill for part of the order. With no lines given it takes everything billable: received and not yet billed on a stocked line, ordered and not yet billed on any other line.
/// AllowUnreceived lets a stocked line be billed ahead of its goods: the debit waits on goods-received clearing until they arrive.
/// </summary>
public sealed record ConvertPurchaseOrderToBillCommand(Guid Id, IReadOnlyDictionary<Guid, decimal>? UnitPriceOverrides = null, IReadOnlyList<BillOrderLineInput>? Lines = null, bool AllowUnreceived = false, string? VendorReference = null) : ICommand<BillDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
