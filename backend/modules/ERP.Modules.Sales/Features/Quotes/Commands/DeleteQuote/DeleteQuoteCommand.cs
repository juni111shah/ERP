using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Deletes a sales quote that is not converted yet.</summary>
public sealed record DeleteQuoteCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.QuotesManage; }
