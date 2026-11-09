using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits a preventive maintenance schedule (it stays on its property).</summary>
public sealed record UpdatePmScheduleCommand(Guid Id, PmScheduleInput Input) : ICommand<PmScheduleDto>, IRequirePermission { public string RequiredPermission => P.PmManage; }
