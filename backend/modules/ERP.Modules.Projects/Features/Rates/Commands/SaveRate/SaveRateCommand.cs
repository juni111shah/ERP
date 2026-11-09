using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Creates (no id) or changes a rate card for a project and/or a person or role.</summary>
public sealed record SaveRateCommand(Guid? Id, RateInput Input) : ICommand<RateDto>, IRequirePermission
{
    public string RequiredPermission => P.RatesManage;
}
