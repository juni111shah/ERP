using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record ChecklistItemInput(string Title, int DueOffsetDays = 0);
public sealed record ChecklistTemplateInput(string Name, ChecklistKind Kind, bool IsActive = true, IReadOnlyList<ChecklistItemInput>? Items = null);
public sealed record ChecklistTemplateDto(Guid Id, string Name, ChecklistKind Kind, bool IsActive, IReadOnlyList<ChecklistItemInput> Items)
{
    public static ChecklistTemplateDto From(ChecklistTemplate t) => new(t.Id, t.Name, t.Kind, t.IsActive, t.Items.OrderBy(x => x.Position).Select(x => new ChecklistItemInput(x.Title, x.DueOffsetDays)).ToList());
}
public sealed record ChecklistTemplateRow(Guid Id, string Name, ChecklistKind Kind, bool IsActive)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<ChecklistTemplate, ChecklistTemplateRow>> Projection = t => new ChecklistTemplateRow(t.Id, t.Name, t.Kind, t.IsActive);
}
