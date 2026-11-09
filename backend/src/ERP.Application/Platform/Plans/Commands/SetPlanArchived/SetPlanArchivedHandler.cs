using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class SetPlanArchivedHandler(ISubscriptionPlanRepository plans, PlanViews views, PlatformSettingsStore settingsStore, PlatformLog log) : ICommandHandler<SetPlanArchivedCommand, PlanView>
{
    public async Task<PlanView> Handle(SetPlanArchivedCommand command, CancellationToken ct)
    {
        var plan = await plans.GetForUpdateAsync(command.Id, ct);
        if (command.Archived && (await settingsStore.GetAsync(ct)).DefaultPlanId == plan.Id) throw new ConflictException("This is the default plan for new registrations. Choose another default first.");
        plan.IsActive = !command.Archived;
        log.Add(command.Archived ? "Plan archived" : "Plan restored", null, null, plan.Name);
        await plans.SaveChangesAsync(ct);
        return await views.ViewAsync(plan, ct);
    }
}
