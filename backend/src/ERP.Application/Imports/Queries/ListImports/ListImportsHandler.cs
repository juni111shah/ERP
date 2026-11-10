using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Imports;

internal sealed class ListImportsHandler(IEnumerable<IImportDefinition> definitions, IModuleAccess modules, ICurrentUser user) : IQueryHandler<ListImportsQuery, IReadOnlyList<ImportDefinitionDto>>
{
    public async Task<IReadOnlyList<ImportDefinitionDto>> Handle(ListImportsQuery query, CancellationToken ct)
    {
        var list = new List<ImportDefinitionDto>();
        foreach (var d in definitions)
        {
            if (!user.HasPermission(d.Permission) || (d.ModuleKey is { } m && !await modules.IsEnabledAsync(m, ct))) continue;
            list.Add(new(d.Key, d.Label, d.ModuleKey, d.Columns));
        }
        return list.OrderBy(d => d.Label).ToList();
    }
}
