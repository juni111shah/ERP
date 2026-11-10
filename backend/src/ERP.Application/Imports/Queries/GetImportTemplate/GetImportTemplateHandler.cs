using ERP.Application.Common;

namespace ERP.Application.Imports;

internal sealed class GetImportTemplateHandler(ImportDefinitions imports) : IQueryHandler<GetImportTemplateQuery, string>
{
    public async Task<string> Handle(GetImportTemplateQuery query, CancellationToken ct)
    {
        var d = await imports.FindAsync(query.Key, ct);
        return string.Join(",", d.Columns.Select(c => Csv.Escape(c.Name))) + "\n" + string.Join(",", d.Columns.Select(c => Csv.Escape(c.Example))) + "\n";
    }
}
