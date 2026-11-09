namespace ERP.Modularity.Features;

public sealed record MenuItemDto(string Id, string Label, string Route, string? Icon, string? Permission, string Module);
public sealed record MenuGroupDto(string Id, string? Label, int Order, IReadOnlyList<MenuItemDto> Items);
