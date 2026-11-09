using ERP.Application.Abstractions;
using ERP.Application.Common;
using FluentValidation;

namespace ERP.Application.Imports;

internal sealed class RunImportHandler(ImportDefinitions imports, IUnitOfWork unitOfWork) : ICommandHandler<RunImportCommand, ImportResultDto>
{
    public const int MaxRows = 2000;

    public async Task<ImportResultDto> Handle(RunImportCommand command, CancellationToken ct)
    {
        // 1. The import exists for the caller; the file has the required columns and not too many rows.
        var d = await imports.FindAsync(command.Key, ct);
        var (header, rows) = Csv.Parse(command.Csv);
        var missing = d.Columns.Where(c => c.Required && !header.Contains(c.Name, StringComparer.OrdinalIgnoreCase)).Select(c => c.Name).ToList();
        if (missing.Count > 0) throw new FieldValidationException("file", $"Missing column(s): {string.Join(", ", missing)}. Download the template for the exact headings.");
        if (rows.Count > MaxRows) throw new FieldValidationException("file", $"At most {MaxRows} rows per file.");

        // 2. Import row by row; rows that fail are reported and skipped.
        var errors = new List<ImportError>();
        var created = 0;
        // A dry run executes every row inside one transaction that is rolled back, so the real rules (duplicates, lookups) are checked and nothing is kept.
        try
        {
            await unitOfWork.InTransactionAsync(async token =>
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (var c = 0; c < header.Count; c++) values[header[c]] = c < rows[i].Count ? rows[i][c] : "";
                    try { await d.ImportRowAsync(new ImportRow(i + 1, values), token); created++; }
                    catch (Exception e) when (e is ImportRowException or FieldValidationException or ConflictException or NotFoundException or ERP.Domain.Common.DomainException or ValidationException)
                    {
                        errors.Add(new(i + 1, e is ValidationException v ? string.Join("; ", v.Errors.Select(x => x.ErrorMessage)) : e.Message));
                    }
                }
                if (command.DryRun) throw new DryRunRollback();
                return 0;
            }, ct);
        }
        catch (DryRunRollback) { /* the transaction was rolled back on purpose */ }
        return new(d.Key, rows.Count, created, errors, command.DryRun);
    }

    private sealed class DryRunRollback : Exception;
}
