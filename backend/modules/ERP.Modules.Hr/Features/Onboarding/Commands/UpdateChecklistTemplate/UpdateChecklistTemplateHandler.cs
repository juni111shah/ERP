using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateChecklistTemplateHandler(IChecklistTemplateRepository templates, IRepository<ChecklistTemplateItem> templateItems) : ICommandHandler<UpdateChecklistTemplateCommand, ChecklistTemplateDto>
{
    public async Task<ChecklistTemplateDto> Handle(UpdateChecklistTemplateCommand command, CancellationToken ct)
    {
        // 1. Load it with its steps; a changed name must still be unique.
        var i = command.Input;
        var t = await templates.GetWithItemsForUpdateAsync(command.Id, ct);
        var name = i.Name.Trim();
        if (name != t.Name && await templates.AnyAsync(x => x.Name == name && x.Id != t.Id, ct)) throw new ConflictException($"A checklist named '{name}' already exists.");

        // 2. Apply, replace the steps when given, and save.
        t.Name = name; t.Kind = i.Kind; t.IsActive = i.IsActive;
        if (i.Items is not null)
        {
            foreach (var old in t.Items.ToList()) templateItems.Remove(old);
            t.Items.Clear();
            ChecklistTemplateMapping.AddItems(t, i);
        }
        await templates.SaveChangesAsync(ct);
        return ChecklistTemplateDto.From(t);
    }
}
