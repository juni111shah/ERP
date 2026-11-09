using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;

namespace ERP.Application.Printing;

/// <summary>Parts shared by the print documents: the company header and the cheque / instrument details.</summary>
public sealed class PrintLookups(IReadRepository<LegalEntity> entities, IReadRepository<Organization> orgs, IReadRepository<PaymentInstrument> instruments, ICurrentUser user)
{
    public async Task<PrintHeaderDto> BuildHeaderAsync(Guid legalEntityId, CancellationToken ct)
    {
        var entity = await entities.FindAsync(legalEntityId, ct)
            ?? throw new NotFoundException("Legal entity not found");
        var org = await orgs.FindAsync(user.OrgId ?? Guid.Empty, ct)
            ?? throw new NotFoundException("Organization not found");

        return new PrintHeaderDto(
            org.Name,
            entity.Name,
            entity.Address,
            entity.TaxRegistrationNo,
            entity.RegistrationNo,
            entity.Phone,
            entity.Email,
            entity.BaseCurrency,
            entity.CountryCode);
    }

    internal async Task<PrintInstrument?> InstrumentAsync(Guid? id, CancellationToken ct)
    {
        if (id is not { } iid) return null;
        var i = await instruments.FindAsync(iid, ct);
        return i is null ? null : new PrintInstrument(i.Kind.ToString(), i.InstrumentNo, i.InstrumentDate, i.Status.ToString(), i.BankName);
    }
}
