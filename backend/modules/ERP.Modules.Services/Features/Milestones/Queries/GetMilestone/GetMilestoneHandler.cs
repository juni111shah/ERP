using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class GetMilestoneHandler(IBillingMilestoneRepository milestones, IInvoiceRepository invoices) : IQueryHandler<GetMilestoneQuery, MilestoneDto>
{
    public async Task<MilestoneDto> Handle(GetMilestoneQuery request, CancellationToken cancellationToken)
    {
        // 1. Load it.
        var milestone = await milestones.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Billing milestone {request.Id} not found.");

        // 2. An invoiced milestone whose invoice is paid is now paid.
        if (milestone.Status == MilestoneStatus.Invoiced && milestone.InvoiceId.HasValue)
        {
            var invoiceStatus = await invoices.GetStatusAsync(milestone.InvoiceId.Value, cancellationToken);
            if (invoiceStatus == DocumentStatus.Paid)
            {
                milestone.Status = MilestoneStatus.Paid;
                await milestones.SaveChangesAsync(cancellationToken);
            }
        }

        return MilestoneDto.From(milestone);
    }
}
