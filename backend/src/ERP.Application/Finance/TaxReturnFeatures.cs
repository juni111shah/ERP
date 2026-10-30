using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record TaxReturnDto(string CountryCode, string Code, string Title, string TitleAr, DateOnly From, DateOnly To, IReadOnlyList<TaxReturnRow> Rows, decimal NetPayable, string CurrencyCode, string? Notes);

/// <summary>
/// The periodic tax return of a legal entity, laid out by its country pack. Sales are issued invoices and credit notes (credit notes negative);
/// purchases are posted bills. A legal entity is required because the country, and so the layout, comes from it.
/// </summary>
public sealed record GetTaxReturn(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<TaxReturnDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

public sealed class GetTaxReturnValidator : AbstractValidator<GetTaxReturn>
{
    public GetTaxReturnValidator()
    {
        RuleFor(x => x.LegalEntityId).NotNull().WithMessage("Choose a legal entity: its country decides the return layout.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
    }
}

/// <summary>Aggregates the period's tax per tax code, in the same way as the tax summary report, but keeps non-recoverable purchases (flagged) so a return can show all of them.</summary>
public sealed class TaxLineReader(IReadRepository<Invoice> invoices, IReadRepository<Bill> bills, IReadRepository<TaxCode> taxCodes)
{
    public async Task<IReadOnlyList<TaxLine>> ReadAsync(DateOnly from, DateOnly to, Guid entityId, CancellationToken ct)
    {
        var sales = await invoices.Query(new Specification<Invoice>(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.PendingApproval && i.Status != DocumentStatus.Voided && i.LegalEntityId == entityId))
            .SelectMany(i => i.Lines, (i, l) => new { i.IsCreditNote, l.TaxCodeId, l.Net, l.TaxAmount }).ToListAsync(ct);
        var purchases = await bills.Query(new Specification<Bill>(b => b.BillDate >= from && b.BillDate <= to && (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid || b.Status == DocumentStatus.Paid) && b.LegalEntityId == entityId))
            .SelectMany(b => b.Lines, (b, l) => new { l.TaxCodeId, l.Net, l.TaxAmount, l.TaxRecoverable }).ToListAsync(ct);
        var codes = await taxCodes.Query().Select(t => new { t.Id, t.Code, t.Kind, t.Rate, t.IsRecoverable }).ToDictionaryAsync(t => t.Id, ct);

        var result = new List<TaxLine>();
        foreach (var g in sales.GroupBy(s => s.TaxCodeId))
        {
            var c = g.Key is { } k && codes.TryGetValue(k, out var found) ? found : null;
            result.Add(new(TaxSide.Sales, c?.Code ?? "-", c?.Kind ?? TaxKind.Other, c?.Rate ?? 0, c?.IsRecoverable ?? true, g.Sum(x => x.IsCreditNote ? -x.Net : x.Net), g.Sum(x => x.IsCreditNote ? -x.TaxAmount : x.TaxAmount)));
        }
        foreach (var g in purchases.GroupBy(p => (p.TaxCodeId, p.TaxRecoverable)))
        {
            var c = g.Key.TaxCodeId is { } k && codes.TryGetValue(k, out var found) ? found : null;
            result.Add(new(TaxSide.Purchases, c?.Code ?? "-", c?.Kind ?? TaxKind.Other, c?.Rate ?? 0, g.Key.TaxRecoverable, g.Sum(x => x.Net), g.Sum(x => x.TaxAmount)));
        }
        return result;
    }
}

internal sealed class TaxReturnHandler(TaxLineReader reader, IReadRepository<LegalEntity> entities, CountryPackRegistry packs) : IRequestHandler<GetTaxReturn, TaxReturnDto>
{
    public async Task<TaxReturnDto> Handle(GetTaxReturn q, CancellationToken ct)
    {
        var entityId = q.LegalEntityId ?? throw new FieldValidationException("legalEntityId", "Choose a legal entity.");
        var entity = await entities.FindAsync(entityId, ct) ?? throw new NotFoundException("Legal entity not found.");
        var pack = packs.Find(entity.CountryCode);
        var def = pack?.TaxReturn ?? throw new ConflictException($"No tax return layout is installed for {entity.CountryCode}. Use the tax summary report instead.");
        var lines = await reader.ReadAsync(q.From, q.To, entityId, ct);
        var (rows, net) = TaxReturnCalculator.Compute(def, lines);
        return new(entity.CountryCode, def.Code, def.Title, def.TitleAr, q.From, q.To, rows, net, entity.BaseCurrency, def.Notes);
    }
}
