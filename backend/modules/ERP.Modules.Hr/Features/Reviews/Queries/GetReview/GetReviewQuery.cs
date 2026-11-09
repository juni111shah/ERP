using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One performance review with its competencies.</summary>
public sealed record GetReviewQuery(Guid Id) : IQuery<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsRead; }
