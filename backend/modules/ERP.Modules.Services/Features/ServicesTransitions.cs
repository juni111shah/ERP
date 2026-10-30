using ERP.Application.Common;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public static class ServicesTransitions
{
    public static readonly StatusTransitions<MilestoneStatus> Milestone = new(
        new Dictionary<MilestoneStatus, IEnumerable<MilestoneStatus>>
        {
            [MilestoneStatus.Planned] = [MilestoneStatus.ReadyToInvoice],
            [MilestoneStatus.ReadyToInvoice] = [MilestoneStatus.Planned, MilestoneStatus.Invoiced],
            [MilestoneStatus.Invoiced] = [MilestoneStatus.Paid, MilestoneStatus.ReadyToInvoice],
            [MilestoneStatus.Paid] = [MilestoneStatus.Invoiced],
        },
        systemOnly: [MilestoneStatus.Invoiced, MilestoneStatus.Paid]);

    public static readonly StatusTransitions<EngagementStatus> Engagement = new(
        new Dictionary<EngagementStatus, IEnumerable<EngagementStatus>>
        {
            [EngagementStatus.Draft] = [EngagementStatus.Active, EngagementStatus.Cancelled],
            [EngagementStatus.Active] = [EngagementStatus.OnHold, EngagementStatus.Completed, EngagementStatus.Cancelled],
            [EngagementStatus.OnHold] = [EngagementStatus.Active, EngagementStatus.Cancelled],
            [EngagementStatus.Completed] = [],
            [EngagementStatus.Cancelled] = [],
        });

    public static readonly StatusTransitions<RetainerStatus> Retainer = new(
        new Dictionary<RetainerStatus, IEnumerable<RetainerStatus>>
        {
            [RetainerStatus.Active] = [RetainerStatus.Paused, RetainerStatus.Terminated],
            [RetainerStatus.Paused] = [RetainerStatus.Active, RetainerStatus.Terminated],
            [RetainerStatus.Terminated] = [],
        });
}
