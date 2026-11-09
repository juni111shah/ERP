using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>Rules shared by creating and editing milestones.</summary>
public sealed class MilestoneRules(IProjectMilestoneRepository milestones, IProjectChangeOrderRepository changeOrders)
{
    /// <summary>Pending milestones carry no achieved date; achieved ones default to today and cannot be dated in the future.</summary>
    public static DateOnly? AchievedDateFor(MilestoneInput i)
    {
        if (i.Status is MilestoneStatus.Pending or MilestoneStatus.Cancelled) return null;
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var date = i.AchievedDate ?? today;
        if (date > today) throw new FieldValidationException("achievedDate", "The achieved date cannot be in the future.");
        return date;
    }

    /// <summary>On a fixed-fee project the milestones together cannot ask for more than the fee (contract value, or the budget when none is set, plus approved change orders).</summary>
    public async Task EnsureWithinFeeAsync(Project p, Guid? exceptId, MilestoneInput i, CancellationToken ct)
    {
        if (p.BillingType != ProjectBillingType.FixedFee || ProjectMath.ContractBase(p) <= 0 || i.Status == MilestoneStatus.Cancelled) return;
        var fee = ProjectMath.Fee(p, await changeOrders.ListForProjectAsync(p.Id, ct));
        var others = await milestones.SumLiveAmountAsync(p.Id, exceptId, ct);
        if (others + i.Amount > fee) throw new FieldValidationException("amount", $"The milestones would add up to {others + i.Amount:N2}, more than the fixed fee of {fee:N2}.");
    }
}
