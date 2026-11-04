using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Wps;

/// <summary>Picks the salary-file builder by the employing legal entity's country (or the configurable CSV when none exists) and builds the file for a posted or paid run.</summary>
internal sealed class WpsFileHandlers(
    IReadRepository<PayrollRun> runs, IReadRepository<Employee> employees, ISettingsService settings, ILegalEntityGuard guard, IClock clock,
    IEnumerable<IWpsFileBuilder> builders) : IRequestHandler<Features.GetPayrollWpsFile, string>
{
    public async Task<string> Handle(Features.GetPayrollWpsFile q, CancellationToken ct)
    {
        var run = await runs.Query(new Specification<PayrollRun>(r => r.Id == q.Id).Include(r => r.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (run.Status is PayrollStatus.Draft or PayrollStatus.PendingApproval) throw new ConflictException("Post the payroll before preparing the WPS file.");
        var entity = await guard.RequireAsync(run.LegalEntityId, ct);
        var ids = run.Lines.Select(l => l.EmployeeId).ToList();
        var staff = (await employees.Query(new Specification<Employee>(e => ids.Contains(e.Id))).ToListAsync(ct)).ToDictionary(e => e.Id);
        var s = new WpsSettings(entity.BaseCurrency, clock.UtcNow,
            (await settings.GetAsync<string>("hr.employerEstablishmentId", run.LegalEntityId, ct))?.Trim(),
            (await settings.GetAsync<string>("hr.employerBankRoutingCode", run.LegalEntityId, ct))?.Trim(),
            (await settings.GetAsync<string>("hr.wps.columns", run.LegalEntityId, ct))?.Trim(),
            await settings.GetAsync<string>("hr.wps.header", run.LegalEntityId, ct));
        var input = new WpsInput(run, run.Lines.ToList(), staff, s);
        var builder = builders.FirstOrDefault(b => string.Equals(b.CountryCode, entity.CountryCode, StringComparison.OrdinalIgnoreCase));
        if (builder is not null) return builder.Build(input);
        if (ConfigurableCsvWpsBuilder.IsConfigured(s)) return new ConfigurableCsvWpsBuilder().Build(input);
        throw new ConflictException($"No salary-file format is set up for {entity.CountryCode}.");
    }
}
