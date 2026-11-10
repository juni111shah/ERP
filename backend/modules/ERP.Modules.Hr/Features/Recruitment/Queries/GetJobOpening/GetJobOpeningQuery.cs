using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One vacancy with its candidate counts.</summary>
public sealed record GetJobOpeningQuery(Guid Id) : IQuery<JobOpeningDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentRead; }
