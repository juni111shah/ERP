using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>The employee linked to the signed-in account, for the self-service handlers.</summary>
public sealed class MyEmployee(IEmployeeRepository employees, ILeaveRequestRepository requests, ICurrentUser user)
{
    /// <summary>The employee linked to the signed-in account; 404 when there is none.</summary>
    public async Task<Employee> GetAsync(CancellationToken ct)
    {
        var uid = user.UserId;
        var me = uid is null ? null : await employees.FindByUserAsync(uid.Value, ct);
        return me ?? throw new NotFoundException("No employee is linked to your account.");
    }

    /// <summary>Someone else's request looks like a missing one, so ids cannot be probed.</summary>
    public async Task RequireMineAsync(Guid requestId, CancellationToken ct)
    {
        var me = (await GetAsync(ct)).Id;
        if (!await requests.AnyAsync(r => r.Id == requestId && r.EmployeeId == me, ct)) throw new NotFoundException("Leave request not found.");
    }
}
