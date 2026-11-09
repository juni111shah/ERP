using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetSignupConfigHandler(ISubscriptionPlanRepository plans, PlatformSettingsStore settingsStore, SignupWorkflow workflow) : IQueryHandler<GetSignupConfigQuery, SignupConfig>
{
    public async Task<SignupConfig> Handle(GetSignupConfigQuery query, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var list = await plans.ListPublicAsync(ct);
        return new(settings.SignupMode, workflow.Verifies(settings), settings.DefaultPlanId, settings.Currency, settings.SupportEmail, settings.TermsUrl,
            list.Select(p => new PublicPlan(p.Id, p.Code, p.Name, p.Description, p.MonthlyPrice, p.AnnualPrice, settings.Currency, p.TrialDays, p.MaxUsers, p.MaxLegalEntities, p.ModuleList(), p.FeatureList(), p.IsRecommended)).ToList());
    }
}
