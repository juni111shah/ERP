using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using FluentValidation;
using MediatR;

namespace ERP.Application.Imports;

public sealed record ImportColumn(string Name, bool Required, string Example, string? Hint = null);
public sealed record ImportError(int Row, string Message);
public sealed record ImportResultDto(string Key, int Total, int Created, IReadOnlyList<ImportError> Errors, bool DryRun);
public sealed record ImportDefinitionDto(string Key, string Label, string? ModuleKey, IReadOnlyList<ImportColumn> Columns);

/// <summary>One parsed CSV row: values by (case-insensitive) column name, and the 1-based data row number for error messages.</summary>
public sealed class ImportRow(int number, IReadOnlyDictionary<string, string> values)
{
    public int Number { get; } = number;
    public string? Get(string column) => values.TryGetValue(column, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
    public string Require(string column) => Get(column) ?? throw new ImportRowException($"'{column}' is required.");
    public decimal? Decimal(string column) => Get(column) is { } v ? (decimal.TryParse(v, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : throw new ImportRowException($"'{column}' must be a number.")) : null;
    public int? Int(string column) => Get(column) is { } v ? (int.TryParse(v, out var i) ? i : throw new ImportRowException($"'{column}' must be a whole number.")) : null;
    public T Enum<T>(string column, T? fallback = null) where T : struct, Enum
        => Get(column) is { } v ? (System.Enum.TryParse<T>(v.Replace(" ", ""), true, out var e) ? e : throw new ImportRowException($"'{column}' must be one of: {string.Join(", ", System.Enum.GetNames<T>())}.")) : fallback ?? throw new ImportRowException($"'{column}' is required.");
}

public sealed class ImportRowException(string message) : Exception(message);

/// <summary>
/// A kind of data that can be bulk-loaded from CSV (parties, items, properties, units ...). Modules register their own.
/// Each row is imported through the normal commands, so every business rule and validation applies; rows that fail are reported and skipped.
/// </summary>
public interface IImportDefinition
{
    string Key { get; }
    string Label { get; }
    string? ModuleKey { get; }
    string Permission { get; }
    IReadOnlyList<ImportColumn> Columns { get; }
    /// <summary>Imports one row (creates the record). Throw <see cref="ImportRowException"/> or any validation/conflict exception to reject it.</summary>
    Task ImportRowAsync(ImportRow row, CancellationToken ct);
}

public static class Csv
{
    /// <summary>RFC 4180 parser: quoted fields, doubled quotes, commas and line breaks inside quotes. Returns the header and the data rows.</summary>
    public static (List<string> Header, List<List<string>> Rows) Parse(string text)
    {
        var records = new List<List<string>>();
        var field = new StringBuilder(); var row = new List<string>(); var inQuotes = false;
        text = text.TrimStart('﻿');
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') inQuotes = false;
                else field.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                if (row.Any(c => c.Length > 0)) records.Add(row);
                row = new List<string>();
            }
            else field.Append(ch);
        }
        row.Add(field.ToString());
        if (row.Any(c => c.Length > 0)) records.Add(row);
        if (records.Count == 0) return ([], []);
        return (records[0].Select(h => h.Trim()).ToList(), records.Skip(1).ToList());
    }

    public static string Escape(string v) => v.Contains(',') || v.Contains('"') || v.Contains('\n') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
}

public sealed record ListImports : IQuery<IReadOnlyList<ImportDefinitionDto>>;
public sealed record GetImportTemplate(string Key) : IQuery<string>;
/// <summary>DryRun checks the file (columns, required values, types, and the rules of every row up to the point of saving) without keeping anything.</summary>
public sealed record RunImport(string Key, string Csv, bool DryRun) : ICommand<ImportResultDto>;

public sealed class RunImportValidator : AbstractValidator<RunImport>
{
    public RunImportValidator()
    {
        RuleFor(x => x.Key).NotEmpty();
        RuleFor(x => x.Csv).NotEmpty().WithMessage("The file is empty.").MaximumLength(5_000_000).WithMessage("The file is too large (5 MB maximum).");
    }
}

internal sealed class ImportHandlers(IEnumerable<IImportDefinition> definitions, IModuleAccess modules, ICurrentUser user, IUnitOfWork unitOfWork) :
    IRequestHandler<ListImports, IReadOnlyList<ImportDefinitionDto>>,
    IRequestHandler<GetImportTemplate, string>,
    IRequestHandler<RunImport, ImportResultDto>
{
    public const int MaxRows = 2000;

    public async Task<IReadOnlyList<ImportDefinitionDto>> Handle(ListImports q, CancellationToken ct)
    {
        var list = new List<ImportDefinitionDto>();
        foreach (var d in definitions)
        {
            if (!user.HasPermission(d.Permission) || (d.ModuleKey is { } m && !await modules.IsEnabledAsync(m, ct))) continue;
            list.Add(new(d.Key, d.Label, d.ModuleKey, d.Columns));
        }
        return list.OrderBy(d => d.Label).ToList();
    }

    public async Task<string> Handle(GetImportTemplate q, CancellationToken ct)
    {
        var d = await FindAsync(q.Key, ct);
        return string.Join(",", d.Columns.Select(c => Csv.Escape(c.Name))) + "\n" + string.Join(",", d.Columns.Select(c => Csv.Escape(c.Example))) + "\n";
    }

    public async Task<ImportResultDto> Handle(RunImport cmd, CancellationToken ct)
    {
        var d = await FindAsync(cmd.Key, ct);
        var (header, rows) = Csv.Parse(cmd.Csv);
        var missing = d.Columns.Where(c => c.Required && !header.Contains(c.Name, StringComparer.OrdinalIgnoreCase)).Select(c => c.Name).ToList();
        if (missing.Count > 0) throw new FieldValidationException("file", $"Missing column(s): {string.Join(", ", missing)}. Download the template for the exact headings.");
        if (rows.Count > MaxRows) throw new FieldValidationException("file", $"At most {MaxRows} rows per file.");

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
                if (cmd.DryRun) throw new DryRunRollback();
                return 0;
            }, ct);
        }
        catch (DryRunRollback) { /* the transaction was rolled back on purpose */ }
        return new(d.Key, rows.Count, created, errors, cmd.DryRun);
    }

    private async Task<IImportDefinition> FindAsync(string key, CancellationToken ct)
    {
        var d = definitions.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? throw new NotFoundException();
        if (!user.HasPermission(d.Permission)) throw new ForbiddenException();
        if (d.ModuleKey is { } m && !await modules.IsEnabledAsync(m, ct)) throw new NotFoundException();
        return d;
    }

    private sealed class DryRunRollback : Exception;
}
