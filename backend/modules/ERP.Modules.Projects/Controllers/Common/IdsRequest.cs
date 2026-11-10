namespace ERP.Modules.Projects.Controllers;

/// <summary>The entries a bulk action (submit, write off) applies to.</summary>
public sealed record IdsRequest(IReadOnlyList<Guid> Ids);

/// <summary>Approve or reject the entries; a rejection needs a reason.</summary>
public sealed record DecideRequest(IReadOnlyList<Guid> Ids, bool Approve, string? Reason);
