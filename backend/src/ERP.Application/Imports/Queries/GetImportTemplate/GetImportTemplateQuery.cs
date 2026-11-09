using ERP.Application.Common;

namespace ERP.Application.Imports;

/// <summary>The CSV template of an import: the header row and one example row.</summary>
public sealed record GetImportTemplateQuery(string Key) : IQuery<string>;
