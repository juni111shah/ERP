using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Shows the invoice lines that billing would create, without creating anything.</summary>
public sealed record PreviewProjectBillQuery(BillProjectInput Input) : IQuery<BillPreviewDto>, IRequirePermission
{
    public string RequiredPermission => P.BillingRead;
}
