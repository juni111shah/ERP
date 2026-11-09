using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>
/// Moves the cost of the project's approved time (cost rate x hours) and approved expenses out of project cost recovery, one journal per calendar month of the work, dated the
/// last day worked in that month. Billable work goes to work in progress until it is billed or the project closes; non-billable work (and anything on a project that has already
/// closed, which could never leave work in progress) goes straight to expense. Every entry is posted once; running it again only picks up what is new.
/// </summary>
public sealed record PostProjectCostsCommand(Guid ProjectId) : ICommand<PostCostsResult>, IRequirePermission
{
    public string RequiredPermission => P.BillingManage;
}
