using ERP.Application.Common;
using ERP.Application.Imports;
using ERP.Modularity;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Imports;

/// <summary>CSV bulk import: list what can be imported, download a template, upload a file (dry run first).</summary>
[Route("api/v1/imports")]
[CapabilityGate("import")]
public sealed class ImportsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [Authorize]
    public Task<IReadOnlyList<ImportDefinitionDto>> List(CancellationToken ct)
        => Mediator.Send(new ListImportsQuery(), ct);

    [HttpGet("{key}/template")]
    [Authorize]
    public async Task<IActionResult> GetTemplate(string key, CancellationToken ct)
        => Content(await Mediator.Send(new GetImportTemplateQuery(key), ct), "text/csv");

    /// <summary>Multipart form with the CSV <c>file</c>; <c>dryRun</c> defaults to true.</summary>
    [HttpPost("{key}")]
    [Authorize]
    public async Task<ImportResultDto> Run(string key, bool? dryRun, CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault() ?? throw new FieldValidationException("file", "A CSV file is required.");
        using var reader = new StreamReader(file.OpenReadStream());
        return await Mediator.Send(new RunImportCommand(key, await reader.ReadToEndAsync(ct), dryRun ?? true), ct);
    }
}
