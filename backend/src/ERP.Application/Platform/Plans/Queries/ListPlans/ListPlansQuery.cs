using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The plans with how many clients are on each (platform console).</summary>
public sealed record ListPlansQuery(bool IncludeArchived = true) : IQuery<IReadOnlyList<PlanView>>;
