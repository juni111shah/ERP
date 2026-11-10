using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;
using ERP.Modules.IndustryPacks.Repositories;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class GetRecordHandler(IIndustryRecordRepository records, IRepository<AuditEvent> audit, RecordDtoBuilder dtos, ICurrentUser user, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<GetRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(GetRecordCommand q, CancellationToken ct)
    {
        var def = PackCatalog.Require(q.Module, q.Type);
        var record = await records.GetWithDetailsAsync(q.Module, q.Type, q.Id, ct);
        if (def.Sensitive)
        {
            // Restricted records leave a trace of who opened them; writes are already audited.
            audit.Add(new AuditEvent { OccurredAt = clock.UtcNow, UserId = user.UserId, Action = "Viewed", RecordType = $"{q.Module}.{q.Type}", RecordId = record.Id.ToString(), IpAddress = user.IpAddress });
            await unitOfWork.SaveChangesAsync(ct);
        }
        return dtos.ToDto(record, def, record.Lines);
    }
}
