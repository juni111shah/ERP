using ERP.Application.Common;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public static class ProjectRules
{
    /// <summary>Work can be added to a project until it is completed or cancelled.</summary>
    public static void EnsureOpen(Project p)
    {
        if (p.IsArchived) throw new ConflictException($"Project '{p.Name}' is archived. Restore it first.");
        if (p.Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
            throw new ConflictException($"Project '{p.Name}' is {p.Status.ToString().ToLowerInvariant()}; reopen is not possible, so nothing can be added to it.");
    }

    /// <summary>Time, expenses and billing only happen on real projects, not on templates.</summary>
    public static void EnsureTrackable(Project p)
    {
        EnsureOpen(p);
        if (p.IsTemplate) throw new ConflictException($"'{p.Name}' is a template. Create a project from it to log time or costs.");
    }
}
