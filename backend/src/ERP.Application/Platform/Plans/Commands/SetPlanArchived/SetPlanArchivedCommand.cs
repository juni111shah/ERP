using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Archives a plan (no new clients can take it) or restores it. The default plan for registrations cannot be archived.</summary>
public sealed record SetPlanArchivedCommand(Guid Id, bool Archived) : ICommand<PlanView>;
