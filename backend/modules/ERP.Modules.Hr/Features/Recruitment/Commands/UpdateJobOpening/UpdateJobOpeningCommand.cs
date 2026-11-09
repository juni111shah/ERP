using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates a vacancy.</summary>
public sealed record UpdateJobOpeningCommand(Guid Id, JobOpeningInput Input) : ICommand<JobOpeningDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
