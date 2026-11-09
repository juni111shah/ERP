using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class InspectionRepository(IRepository<Inspection> inner) : RepositoryBase<Inspection>(inner), IInspectionRepository
{
    public async Task<Inspection> GetWithItemsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<Inspection>(x => x.Id == id).Include(x => x.Items)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<Inspection> GetWithItemsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<Inspection>(x => x.Id == id).Include(x => x.Items)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
}
