using ERP.Application.Security;

namespace ERP.Modules.Services;

public static class Permissions
{
    public const string EngagementsRead = "services.engagements.read";
    public const string EngagementsManage = "services.engagements.manage";

    public const string RetainersRead = "services.retainers.read";
    public const string RetainersManage = "services.retainers.manage";

    public const string MilestonesRead = "services.milestones.read";
    public const string MilestonesManage = "services.milestones.manage";

    public const string TimeEntriesRead = "services.timeentries.read";
    public const string TimeEntriesManage = "services.timeentries.manage";

    public const string AnalyticsRead = "services.analytics.read";

    public static readonly IReadOnlyList<PermissionDefinition> Definitions =
    [
        PermissionDefinition.Of(EngagementsRead, "View service engagements, scopes, and contracts"),
        PermissionDefinition.Of(EngagementsManage, "Create, manage, and update service engagements"),
        PermissionDefinition.Of(RetainersRead, "View recurring retainer agreements and terms"),
        PermissionDefinition.Of(RetainersManage, "Create, pause, and configure retainer agreements"),
        PermissionDefinition.Of(MilestonesRead, "View milestone billing schedules and deliverable progress"),
        PermissionDefinition.Of(MilestonesManage, "Create and transition billing milestones to invoice status"),
        PermissionDefinition.Of(TimeEntriesRead, "View consultant timesheets and logged activity records"),
        PermissionDefinition.Of(TimeEntriesManage, "Log hours and approve consultant timesheet submissions"),
        PermissionDefinition.Of(AnalyticsRead, "View professional services utilization, MRR, and milestone pipeline")
    ];
}
