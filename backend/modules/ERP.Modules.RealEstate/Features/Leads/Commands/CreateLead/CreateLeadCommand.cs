using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates a lead in a legal entity the caller may use.</summary>
public sealed record CreateLeadCommand(LeadInput Input) : ICommand<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
