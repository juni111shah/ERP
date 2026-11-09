using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Moves a candidate to another stage of the pipeline (see <see cref="CandidatePipeline"/>), appending the note.</summary>
public sealed record MoveCandidateCommand(Guid Id, MoveCandidateInput Input) : ICommand<CandidateDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
