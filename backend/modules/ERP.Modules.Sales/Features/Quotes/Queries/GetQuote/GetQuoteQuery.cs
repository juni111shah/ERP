using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>One sales quote with its lines.</summary>
public sealed record GetQuoteQuery(Guid Id) : IQuery<QuoteDto>, IRequirePermission { public string RequiredPermission => P.QuotesRead; }
