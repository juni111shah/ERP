using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class ChecklistTemplateRepository(IRepository<ChecklistTemplate> inner) : RepositoryBase<ChecklistTemplate>(inner), IChecklistTemplateRepository
{
    public async Task<ChecklistTemplate> GetWithItemsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<ChecklistTemplate>(t => t.Id == id).Include(t => t.Items)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<ChecklistTemplate> GetWithItemsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<ChecklistTemplate>(x => x.Id == id).Include(x => x.Items)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
}
