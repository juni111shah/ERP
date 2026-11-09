using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates a brokerage deal from the lead (value and type default from its listing), then marks the lead Won.</summary>
public sealed record ConvertLeadToDealCommand(Guid Id, ConvertLeadInput Input) : ICommand<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
