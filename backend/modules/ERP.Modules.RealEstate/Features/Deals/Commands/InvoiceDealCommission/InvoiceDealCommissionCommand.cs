using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Raises and issues the commission invoice to the client (Dr receivables, Cr brokerage income and output tax); the deal becomes Invoiced.</summary>
public sealed record InvoiceDealCommissionCommand(Guid Id) : ICommand<DealDto>, IRequirePermission { public string RequiredPermission => P.DealsInvoice; }
