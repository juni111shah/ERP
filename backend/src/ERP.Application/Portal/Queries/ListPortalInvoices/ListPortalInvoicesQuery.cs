using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>The portal customer's latest invoices and credit notes (drafts, ones pending approval and voided ones left out).</summary>
public sealed record ListPortalInvoicesQuery : IQuery<IReadOnlyList<PortalInvoiceDto>>;
