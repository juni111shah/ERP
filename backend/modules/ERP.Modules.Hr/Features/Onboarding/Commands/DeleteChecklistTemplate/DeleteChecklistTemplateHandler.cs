using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteChecklistTemplateHandler(IChecklistTemplateRepository templates, IRepository<ChecklistTemplateItem> templateItems) : ICommandHandler<DeleteChecklistTemplateCommand>
{
    public async Task<Unit> Handle(DeleteChecklistTemplateCommand command, CancellationToken ct)
    {
        var t = await templates.GetWithItemsForUpdateAsync(command.Id, ct);
        foreach (var item in t.Items.ToList()) templateItems.Remove(item);
        templates.Remove(t);
        await templates.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
