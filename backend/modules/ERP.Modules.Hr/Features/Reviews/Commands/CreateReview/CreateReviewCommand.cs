using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Writes a draft performance review of an employee in an open cycle (one per employee and cycle).</summary>
public sealed record CreateReviewCommand(ReviewInput Input) : ICommand<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
