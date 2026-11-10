using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Imports;

/// <summary>Finds an import the caller may run: it exists, the caller has its permission and its module is enabled.</summary>
public sealed class ImportDefinitions(IEnumerable<IImportDefinition> definitions, IModuleAccess modules, ICurrentUser user)
{
    public async Task<IImportDefinition> FindAsync(string key, CancellationToken ct)
    {
        var d = definitions.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? throw new NotFoundException();
        if (!user.HasPermission(d.Permission)) throw new ForbiddenException();
        if (d.ModuleKey is { } m && !await modules.IsEnabledAsync(m, ct)) throw new NotFoundException();
        return d;
    }
}
