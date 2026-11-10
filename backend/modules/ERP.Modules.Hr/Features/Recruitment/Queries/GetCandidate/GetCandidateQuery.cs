using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One candidate.</summary>
public sealed record GetCandidateQuery(Guid Id) : IQuery<CandidateDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentRead; }
