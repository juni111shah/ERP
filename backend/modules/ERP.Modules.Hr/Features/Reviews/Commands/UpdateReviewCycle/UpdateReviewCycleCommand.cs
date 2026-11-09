using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates a review cycle (open or close it); a changed name stays unique.</summary>
public sealed record UpdateReviewCycleCommand(Guid Id, ReviewCycleInput Input) : ICommand<ReviewCycleDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
