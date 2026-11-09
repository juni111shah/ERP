using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>The definitions of a pack's record types that the caller may read (restricted fields only with the sensitive permission).</summary>
public sealed record GetPackViewQuery(string Module) : IQuery<PackView>;
