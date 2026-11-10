using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateChecklistTemplateHandler(IChecklistTemplateRepository templates) : ICommandHandler<CreateChecklistTemplateCommand, ChecklistTemplateDto>
{
    public async Task<ChecklistTemplateDto> Handle(CreateChecklistTemplateCommand command, CancellationToken ct)
    {
        // 1. The name is unique.
        var i = command.Input;
        var name = i.Name.Trim();
        if (await templates.AnyAsync(t => t.Name == name, ct)) throw new ConflictException($"A checklist named '{name}' already exists.");

        // 2. Add it with its steps, and save.
        var t = new ChecklistTemplate { Name = name, Kind = i.Kind, IsActive = i.IsActive };
        templates.Add(t);
        ChecklistTemplateMapping.AddItems(t, i);
        await templates.SaveChangesAsync(ct);
        return ChecklistTemplateDto.From(t);
    }
}
