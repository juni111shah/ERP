using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Opens a vacancy.</summary>
public sealed record CreateJobOpeningCommand(JobOpeningInput Input) : ICommand<JobOpeningDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
