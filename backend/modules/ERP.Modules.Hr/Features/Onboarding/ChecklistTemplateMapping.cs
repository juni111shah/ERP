using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Adds the input's steps to a checklist template, numbered in order (create and update).</summary>
internal static class ChecklistTemplateMapping
{
    public static void AddItems(ChecklistTemplate t, ChecklistTemplateInput i)
    {
        var n = 0;
        foreach (var item in i.Items ?? []) t.Items.Add(new ChecklistTemplateItem { TemplateId = t.Id, Position = ++n, Title = item.Title.Trim(), DueOffsetDays = item.DueOffsetDays });
    }
}
