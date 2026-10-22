using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record GratuityRow(Guid EmployeeId, string Number, string Employee, Guid LegalEntityId, DateOnly Joined, decimal BasicSalary, decimal ServiceYears, bool Eligible, decimal EntitlementDays, decimal Gratuity);
public sealed record GratuityReportDto(DateOnly AsOf, IReadOnlyList<GratuityRow> Rows, decimal TotalLiability, string? Rule);
public sealed record GratuityProvisionDto(DateOnly AsOf, decimal Liability, decimal Provision, decimal Posted, Guid? JournalId);

/// <summary>End-of-service liability per employee, under the employing entity's country rule.</summary>
public sealed record GetGratuityReport(DateOnly AsOf, Guid? LegalEntityId) : IQuery<GratuityReportDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
/// <summary>Trues the provision up to the gratuity liability: Dr end-of-service expense, Cr provision (reversed when the liability fell).</summary>
public sealed record PostGratuityProvision(Guid LegalEntityId, DateOnly AsOf) : ICommand<GratuityProvisionDto>, IRequirePermission { public string RequiredPermission => P.PayrollPost; }

internal sealed class GratuityHandlers(
    IReadRepository<Employee> employees, IReadRepository<LegalEntity> legalEntities, IReadRepository<JournalEntry> journals, GratuityCalculator calculator, ILegalEntityGuard guard, IPostingService posting, IUnitOfWork uow) :
    IRequestHandler<GetGratuityReport, GratuityReportDto>,
    IRequestHandler<PostGratuityProvision, GratuityProvisionDto>
{
    public async Task<GratuityReportDto> Handle(GetGratuityReport q, CancellationToken ct)
    {
        var (rows, rule) = await BuildAsync(q.AsOf, q.LegalEntityId, ct);
        return new GratuityReportDto(q.AsOf, rows, rows.Sum(r => r.Gratuity), rule);
    }

    public async Task<GratuityProvisionDto> Handle(PostGratuityProvision cmd, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(cmd.LegalEntityId, ct);
        var (rows, _) = await BuildAsync(cmd.AsOf, entity.Id, ct);
        var liability = rows.Sum(r => r.Gratuity);
        var provisionAccount = await posting.AccountForRoleAsync("payroll.gratuity.provision", ct);
        var movements = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.LegalEntityId == entity.Id && j.Date <= cmd.AsOf))
            .SelectMany(j => j.Lines.Where(l => l.AccountId == provisionAccount), (j, l) => new { l.Debit, l.Credit }).ToListAsync(ct);
        var provision = movements.Sum(m => m.Credit - m.Debit);
        var diff = liability - provision;
        if (diff == 0) return new GratuityProvisionDto(cmd.AsOf, liability, provision, 0, null);
        var expense = await posting.AccountForRoleAsync("payroll.gratuity.expense", ct);
        var memo = $"End-of-service provision true-up {cmd.AsOf:yyyy-MM}";
        var amount = Math.Abs(diff);
        var lines = diff > 0 ? new[] { new PostingLine(expense, amount, 0, memo), new PostingLine(provisionAccount, 0, amount, memo) } : new[] { new PostingLine(provisionAccount, amount, 0, memo), new PostingLine(expense, 0, amount, memo) };
        var journal = await posting.PostAsync(new PostingRequest(entity.Id, cmd.AsOf, "gratuity", null, memo, null, lines), ct);
        await uow.SaveChangesAsync(ct);
        return new GratuityProvisionDto(cmd.AsOf, liability, provision + diff, diff, journal.Id);
    }

    private async Task<(List<GratuityRow> Rows, string? Rule)> BuildAsync(DateOnly asOf, Guid? entityId, CancellationToken ct)
    {
        var staff = (await employees.Query(new Specification<Employee>(e => (entityId == null || e.LegalEntityId == entityId) && e.JoinDate <= asOf)).ToListAsync(ct)).Where(e => e.Status != EmployeeStatus.Terminated).OrderBy(e => e.Number).ToList();
        var countries = await legalEntities.Query().Select(l => new { l.Id, l.CountryCode }).ToDictionaryAsync(l => l.Id, l => l.CountryCode, ct);
        string? rule = null;
        var rows = staff.Select(e =>
        {
            var g = calculator.Calculate(e.BasicSalary, e.JoinDate, e.TerminationDate, asOf, countries.GetValueOrDefault(e.LegalEntityId, ""));
            rule ??= g.Rule;
            return new GratuityRow(e.Id, e.Number, e.FullName, e.LegalEntityId, e.JoinDate, e.BasicSalary, Math.Round(g.ServiceYears, 2), g.Eligible, g.EntitlementDays, g.Amount);
        }).ToList();
        return (rows, rule);
    }
}
