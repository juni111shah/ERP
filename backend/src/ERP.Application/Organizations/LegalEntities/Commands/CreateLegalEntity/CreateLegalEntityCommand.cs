using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Organizations;

/// <summary>Adds a legal entity (numbered automatically), within the plan's limit.</summary>
public sealed record CreateLegalEntityCommand(LegalEntityInput Input) : ICommand<LegalEntityDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EntitiesWrite;
}
