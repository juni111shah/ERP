using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>
/// Charges one month for a legal entity: one journal (Dr depreciation expense, Cr accumulated depreciation), dated the last day of the month, grouped by account and business unit.
/// Running a period again changes nothing and returns the first run. A skipped month is caught up by the next run. Depreciation stops at the salvage value.
/// </summary>
public sealed record RunDepreciationCommand(Guid LegalEntityId, int Year, int Month) : ICommand<DepreciationRunDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => Permissions.DepreciationRun;
}
