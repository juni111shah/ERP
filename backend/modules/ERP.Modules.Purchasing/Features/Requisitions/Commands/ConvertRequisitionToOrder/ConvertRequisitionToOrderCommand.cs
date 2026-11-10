using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Turns an approved requisition into a draft purchase order for the vendor.</summary>
public sealed record ConvertRequisitionToOrderCommand(Guid Id, Guid? VendorPartyId, string VendorName) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
