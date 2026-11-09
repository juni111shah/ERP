using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class CreatePlanHandler(ISubscriptionPlanRepository plans, PlanViews views, PlatformLog log) : ICommandHandler<CreatePlanCommand, PlanView>
{
    public async Task<PlanView> Handle(CreatePlanCommand command, CancellationToken ct)
    {
        if (await plans.AnyAsync(p => p.Code == command.Input.Code, ct)) throw new FieldValidationException("code", "That plan code is already used.");
        var plan = new SubscriptionPlan();
        PlanViews.Apply(plan, command.Input);
        plans.Add(plan);
        log.Add("Plan created", null, null, $"{plan.Name}: {plan.MonthlyPrice:N2} a month, {plan.AnnualPrice:N2} a year");
        await plans.SaveChangesAsync(ct);
        return await views.ViewAsync(plan, ct);
    }
}
