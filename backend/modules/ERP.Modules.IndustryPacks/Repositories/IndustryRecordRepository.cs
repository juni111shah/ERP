using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.IndustryPacks.Repositories;

internal sealed class IndustryRecordRepository(IRepository<IndustryRecord> inner) : RepositoryBase<IndustryRecord>(inner), IIndustryRecordRepository
{
    public async Task<IndustryRecord> GetWithDetailsAsync(string module, string type, Guid id, CancellationToken ct)
        => await Query(WithDetails(module, type, id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("That record was not found.");

    public async Task<IndustryRecord> GetWithDetailsForUpdateAsync(string module, string type, Guid id, CancellationToken ct)
        => await Tracked(WithDetails(module, type, id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("That record was not found.");

    public Task<PagedResult<IndustryRecord>> PageOfTypeAsync(string module, string type, PagedQuery paging, CancellationToken ct)
        => PageAsync(paging, new Specification<IndustryRecord>(r => r.ModuleKey == module && r.RecordType == type), ct);

    public Task<List<RecordStatusAmount>> ListStatusAmountsAsync(string module, string[] types, Guid? legalEntityId, CancellationToken ct)
        => Query().Where(r => r.ModuleKey == module && types.Contains(r.RecordType) && (legalEntityId == null || r.LegalEntityId == legalEntityId))
            .Select(r => new RecordStatusAmount(r.RecordType, r.Status, r.Amount)).ToListAsync(ct);

    public Task<RecordParent?> FindParentAsync(Guid parentId, string module, string parentType, CancellationToken ct)
        => Query().Where(r => r.Id == parentId && r.ModuleKey == module && r.RecordType == parentType).Select(r => new RecordParent(r.Id, r.Number, r.Title, r.LegalEntityId)).FirstOrDefaultAsync(ct);

    public Task<IndustryRecord?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<IndustryRecord>(r => r.Id == id).Include(r => r.Lines)).FirstOrDefaultAsync(ct);

    public Task<IndustryRecord?> FindByBillForUpdateAsync(Guid billId, CancellationToken ct)
        => Tracked(new Specification<IndustryRecord>(r => r.BillId == billId)).FirstOrDefaultAsync(ct);

    private static Specification<IndustryRecord> WithDetails(string module, string type, Guid id)
        => new Specification<IndustryRecord>(r => r.Id == id && r.ModuleKey == module && r.RecordType == type).Include(r => r.Lines).Include(r => r.Events);
}
