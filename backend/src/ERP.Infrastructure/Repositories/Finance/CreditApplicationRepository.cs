using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class CreditApplicationRepository(IRepository<CreditApplication> creditApplications)
    : RepositoryBase<CreditApplication>(creditApplications), ICreditApplicationRepository
{
    public Task<List<CreditApplicationAmount>> ListAsOfAsync(DateOnly asOf, Guid? entityId, CancellationToken ct)
        => Query(new Specification<CreditApplication>(c => c.Date <= asOf && (entityId == null || c.LegalEntityId == entityId)))
            .Select(c => new CreditApplicationAmount(c.CreditNoteId, c.InvoiceId, c.Amount)).ToListAsync(ct);
}
