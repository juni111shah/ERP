using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates a review cycle (unique name) for a period.</summary>
public sealed record CreateReviewCycleCommand(ReviewCycleInput Input) : ICommand<ReviewCycleDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
