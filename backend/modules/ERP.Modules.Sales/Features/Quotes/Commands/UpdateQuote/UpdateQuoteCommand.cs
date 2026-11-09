using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Replaces a sales quote that is not converted yet.</summary>
public sealed record UpdateQuoteCommand(Guid Id, QuoteInput Input) : ICommand<QuoteDto>, IRequirePermission { public string RequiredPermission => P.QuotesManage; }
