using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Creates a draft sales quote.</summary>
public sealed record CreateQuoteCommand(QuoteInput Input) : ICommand<QuoteDto>, IRequirePermission { public string RequiredPermission => P.QuotesManage; }
