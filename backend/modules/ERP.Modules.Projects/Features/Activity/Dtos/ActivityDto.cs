namespace ERP.Modules.Projects.Features;

public sealed record ActivityDto(DateTime At, string? Who, string Action, string Kind, string Title, IReadOnlyList<string> Fields);
