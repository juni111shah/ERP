using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

/// <summary>Moves a sales quote to another status by hand (sent, accepted, rejected...).</summary>
public sealed record ChangeQuoteStatusCommand(Guid Id, SalesQuoteStatus Status) : ICommand<QuoteDto>, IRequirePermission { public string RequiredPermission => P.QuotesManage; }
