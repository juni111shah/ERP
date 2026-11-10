using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class ApproveTimeEntryHandler(ITimeEntryRecordRepository time, ICurrentUser user) : ICommandHandler<ApproveTimeEntryCommand, TimeEntryDto>
{
    public async Task<TimeEntryDto> Handle(ApproveTimeEntryCommand request, CancellationToken cancellationToken)
    {
        // 1. Approved once, by a signed-in user who is not the consultant.
        var entry = await time.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Time entry {request.Id} not found.");
        if (entry.IsApproved) throw new ConflictException("This time entry is already approved.");
        if (user.UserId is not { } approver) throw new ForbiddenException("Approving time needs a signed-in user.");
        if (approver == entry.ConsultantUserId) throw new ConflictException("You cannot approve your own time. Ask another approver.");

        // 2. Approve and save.
        entry.IsApproved = true;
        entry.ApprovedByUserId = approver;
        entry.ApprovedAt = ERP.Domain.Common.AppClock.UtcNow;

        await time.SaveChangesAsync(cancellationToken);
        return TimeEntryDto.From(entry);
    }
}
