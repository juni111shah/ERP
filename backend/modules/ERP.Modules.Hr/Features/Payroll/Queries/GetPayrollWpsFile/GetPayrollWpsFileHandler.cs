using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;
using ERP.Modules.Hr.Wps;

namespace ERP.Modules.Hr.Features;

/// <summary>Picks the salary-file builder by the employing legal entity's country (or the configurable CSV when none exists) and builds the file for a posted or paid run.</summary>
internal sealed class GetPayrollWpsFileHandler(
    IPayrollRunRepository runs, IEmployeeRepository employees, ISettingsService settings, ILegalEntityGuard guard, IClock clock,
    IEnumerable<IWpsFileBuilder> builders) : IQueryHandler<GetPayrollWpsFileQuery, string>
{
    public async Task<string> Handle(GetPayrollWpsFileQuery q, CancellationToken ct)
    {
        // 1. Only a posted (or paid) run; optionally only the unpaid lines.
        var run = await runs.GetWithLinesAsync(q.Id, ct);
        if (run.Status is PayrollStatus.Draft or PayrollStatus.PendingApproval) throw new ConflictException("Post the payroll before preparing the WPS file.");
        if (run.Status == PayrollStatus.Voided) throw new ConflictException("This payroll was voided.");
        var fileLines = q.UnpaidOnly ? run.Lines.Where(l => l.PaidOn is null && run.Status != PayrollStatus.Paid).ToList() : run.Lines.ToList();

        // 2. The employees on the file and the employing entity's salary-file settings.
        var entity = await guard.RequireAsync(run.LegalEntityId, ct);
        var ids = fileLines.Select(l => l.EmployeeId).ToList();
        var staff = await employees.MapByIdsAsync(ids, ct);
        var s = new WpsSettings(entity.BaseCurrency, clock.UtcNow,
            (await settings.GetAsync<string>("hr.employerEstablishmentId", run.LegalEntityId, ct))?.Trim(),
            (await settings.GetAsync<string>("hr.employerBankRoutingCode", run.LegalEntityId, ct))?.Trim(),
            (await settings.GetAsync<string>("hr.wps.columns", run.LegalEntityId, ct))?.Trim(),
            await settings.GetAsync<string>("hr.wps.header", run.LegalEntityId, ct));

        // 3. The country's format, else the configured CSV.
        var input = new WpsInput(run, fileLines, staff, s);
        var builder = builders.FirstOrDefault(b => string.Equals(b.CountryCode, entity.CountryCode, StringComparison.OrdinalIgnoreCase));
        if (builder is not null) return builder.Build(input);
        if (ConfigurableCsvWpsBuilder.IsConfigured(s)) return new ConfigurableCsvWpsBuilder().Build(input);
        throw new ConflictException($"No salary-file format is set up for {entity.CountryCode}.");
    }
}
