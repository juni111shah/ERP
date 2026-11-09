using ERP.Application.Common;

namespace ERP.Application.Imports;

/// <summary>What the caller may import (permission held, module enabled), by label.</summary>
public sealed record ListImportsQuery : IQuery<IReadOnlyList<ImportDefinitionDto>>;
