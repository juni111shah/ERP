using ERP.Application.Finance;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;
using MediatR;

namespace ERP.Modules.Services.Features;

public class MilestonePaymentNotificationHandler(
    IBillingMilestoneRepository milestones) :
    INotificationHandler<InvoicePaymentChanged>,
    INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "services.milestone";

    public async Task Handle(InvoicePaymentChanged n, CancellationToken ct)
    {
        var mls = await milestones.ListForInvoiceForUpdateAsync(n.InvoiceId, n.SourceId, ct);
        foreach (var m in mls)
        {
            if (n.IsPaid)
            {
                m.Status = MilestoneStatus.Paid;
            }
            else if (m.Status == MilestoneStatus.Paid)
            {
                m.Status = MilestoneStatus.Invoiced;
            }
        }
        if (mls.Count > 0)
        {
            await milestones.SaveChangesAsync(ct);
        }
    }

    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        var mls = await milestones.ListForInvoiceForUpdateAsync(n.InvoiceId, n.SourceId, ct);
        foreach (var m in mls)
        {
            m.Status = MilestoneStatus.ReadyToInvoice;
            m.InvoiceId = null;
        }
        if (mls.Count > 0)
        {
            await milestones.SaveChangesAsync(ct);
        }
    }
}
