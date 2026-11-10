using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits a lead that is not won; its legal entity is fixed.</summary>
public sealed record UpdateLeadCommand(Guid Id, LeadInput Input) : ICommand<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
