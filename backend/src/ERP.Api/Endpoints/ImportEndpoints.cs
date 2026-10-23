using ERP.Application.Common;
using ERP.Application.Imports;
using MediatR;

namespace ERP.Api.Endpoints;

/// <summary>CSV bulk import: list what can be imported, download a template, upload a file (dry run first).</summary>
public static class ImportEndpoints
{
    public static void MapImports(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/imports").WithTags("Imports");
        g.MapGet("", (ISender s, CancellationToken ct) => s.Send(new ListImports(), ct)).RequireAuthorization();
        g.MapGet("/{key}/template", async (string key, ISender s, CancellationToken ct) => Results.Text(await s.Send(new GetImportTemplate(key), ct), "text/csv")).RequireAuthorization();
        g.MapPost("/{key}", async (string key, bool? dryRun, HttpRequest request, ISender s, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault() ?? throw new FieldValidationException("file", "A CSV file is required.");
            using var reader = new StreamReader(file.OpenReadStream());
            return await s.Send(new RunImport(key, await reader.ReadToEndAsync(ct), dryRun ?? true), ct);
        }).RequireAuthorization().DisableAntiforgery();
    }
}
