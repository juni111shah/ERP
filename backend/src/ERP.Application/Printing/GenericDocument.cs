namespace ERP.Application.Printing;

public sealed record PrintGenericColumn(string Title, float Relative, bool Numeric = false);
public sealed record PrintGenericTotal(string Label, string Value, bool Strong = false);

/// <summary>
/// A document a module has already prepared as plain text: header block, title and number, an optional party, key/value details, a table and totals.
/// Lets any module get the standard PDF and email without the core knowing what the document is.
/// </summary>
public sealed record PrintGenericDto(
    PrintHeaderDto Header, string Title, string Number, string? Stamp, string? PartyLabel, string? PartyName, IReadOnlyList<string> PartyLines,
    IReadOnlyList<(string Label, string Value)> Meta, IReadOnlyList<PrintGenericColumn> Columns, IReadOnlyList<string[]> Rows, IReadOnlyList<PrintGenericTotal> Totals, string? Notes);
