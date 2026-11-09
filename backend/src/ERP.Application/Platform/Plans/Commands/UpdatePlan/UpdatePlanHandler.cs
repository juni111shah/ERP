using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class UpdatePlanHandler(ISubscriptionPlanRepository plans, PlanViews views, PlatformLog log) : ICommandHandler<UpdatePlanCommand, PlanView>
{
    public async Task<PlanView> Handle(UpdatePlanCommand command, CancellationToken ct)
    {
        var plan = await plans.GetForUpdateAsync(command.Id, ct);
        if (await plans.AnyAsync(p => p.Code == command.Input.Code && p.Id != command.Id, ct)) throw new FieldValidationException("code", "That plan code is already used.");
        var was = (plan.MonthlyPrice, plan.AnnualPrice);
        PlanViews.Apply(plan, command.Input);
        log.Add("Plan updated", null, null, was == (plan.MonthlyPrice, plan.AnnualPrice) ? plan.Name : $"{plan.Name}: price {was.MonthlyPrice:N2}/{was.AnnualPrice:N2} changed to {plan.MonthlyPrice:N2}/{plan.AnnualPrice:N2}");
        await plans.SaveChangesAsync(ct);
        return await views.ViewAsync(plan, ct);
    }
}
