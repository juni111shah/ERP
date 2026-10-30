using ERP.Application.Security;

namespace ERP.Modules.Crm;

public static class Permissions
{
    public const string LeadsRead = "crm.leads.read";
    public const string LeadsManage = "crm.leads.manage";
    public const string OpportunitiesRead = "crm.opportunities.read";
    public const string OpportunitiesManage = "crm.opportunities.manage";
    public const string InteractionsRead = "crm.interactions.read";
    public const string InteractionsManage = "crm.interactions.manage";
    public const string AnalyticsRead = "crm.analytics.read";

    public static readonly IReadOnlyList<PermissionDefinition> Definitions =
    [
        PermissionDefinition.Of(LeadsRead, "View CRM leads and prospect contacts"),
        PermissionDefinition.Of(LeadsManage, "Create, update, qualify, and convert CRM leads"),
        PermissionDefinition.Of(OpportunitiesRead, "View sales pipeline deals and opportunities"),
        PermissionDefinition.Of(OpportunitiesManage, "Create, progress, and close sales opportunities"),
        PermissionDefinition.Of(InteractionsRead, "View customer communication logs and touchpoints"),
        PermissionDefinition.Of(InteractionsManage, "Log customer calls, emails, meetings, and notes"),
        PermissionDefinition.Of(AnalyticsRead, "View CRM pipeline analytics, deal velocity, and win rate")
    ];
}
