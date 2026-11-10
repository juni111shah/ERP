using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Hires a candidate with an offer: creates the employee and, when asked, applies an onboarding checklist.</summary>
public sealed record HireCandidateCommand(Guid Id, HireInput Input) : ICommand<HireResult>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
