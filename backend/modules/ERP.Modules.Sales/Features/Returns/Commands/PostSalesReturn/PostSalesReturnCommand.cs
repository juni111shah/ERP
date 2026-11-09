using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Restocks at the cost the goods left at (Dr inventory, Cr cost of sales) and, when the goods were invoiced, issues the credit note. It stays unapplied until someone applies it, as any credit note does.</summary>
public sealed record PostSalesReturnCommand(Guid Id) : ICommand<SalesReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
