using ERP.Application.Common;
using ERP.Application.Finance;

namespace ERP.Modules.Services.Features;

/// <summary>Raises the draft invoice of a milestone that is ready to invoice.</summary>
public record InvoiceMilestoneCommand(Guid Id) : ICommand<InvoiceDto>;
