using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Adds a candidate to an open vacancy (stage Applied).</summary>
public sealed record CreateCandidateCommand(CandidateInput Input) : ICommand<CandidateDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
