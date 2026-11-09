using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;

namespace ERP.Modules.Services.Features;

/// <summary>Invoices the approved, billable, not yet invoiced time of a time-and-materials engagement, one line per entry. Ids (or a date range) limit it; none means everything ready. Voiding the invoice makes the time billable again.</summary>
public sealed record InvoiceEngagementTimeCommand(Guid EngagementId, IReadOnlyList<Guid>? TimeEntryIds = null, DateOnly? From = null, DateOnly? To = null, DateOnly? InvoiceDate = null, DateOnly? DueDate = null, Guid? TaxCodeId = null, string? Notes = null)
    : ICommand<InvoiceDto>, IRequirePermission { public string RequiredPermission => Permissions.EngagementsManage; }
