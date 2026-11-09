using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates a preventive maintenance schedule on a property or unit.</summary>
public sealed record CreatePmScheduleCommand(PmScheduleInput Input) : ICommand<PmScheduleDto>, IRequirePermission { public string RequiredPermission => P.PmManage; }
