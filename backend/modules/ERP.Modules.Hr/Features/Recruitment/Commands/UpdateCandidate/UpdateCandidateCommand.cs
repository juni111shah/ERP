using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates a candidate; they stay on their vacancy.</summary>
public sealed record UpdateCandidateCommand(Guid Id, CandidateInput Input) : ICommand<CandidateDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
