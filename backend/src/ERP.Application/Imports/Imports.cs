using System.Text;
using ERP.Application.Common;

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
