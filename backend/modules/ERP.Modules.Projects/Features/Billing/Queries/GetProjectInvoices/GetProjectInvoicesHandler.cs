using ERP.Application.Common;
using ERP.Application.Finance;

namespace ERP.Modules.Projects.Features;

internal sealed class GetProjectInvoicesHandler(IInvoiceRepository invoices, ProjectAccess access) : IQueryHandler<GetProjectInvoicesQuery, IReadOnlyList<ProjectInvoiceDto>>
{
    public async Task<IReadOnlyList<ProjectInvoiceDto>> Handle(GetProjectInvoicesQuery query, CancellationToken ct)
    {
        await access.EnsureProjectAsync(query.ProjectId, ct);
        var id = query.ProjectId;
        var rows = await invoices.ListForSourceAsync(ProjectInvoiceVoidedHandler.SourceType, id, ct);
        return rows.OrderByDescending(x => x.InvoiceDate).Select(x => new ProjectInvoiceDto(x.Id, x.Number, x.InvoiceDate, x.DueDate, x.Status, x.Total, x.Paid)).ToList();
    }
}
