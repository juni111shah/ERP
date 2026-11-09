using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Organizations;

/// <summary>Changes a legal entity's details and custom fields.</summary>
public sealed record UpdateLegalEntityCommand(Guid Id, LegalEntityInput Input) : ICommand<LegalEntityDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EntitiesWrite;
}
