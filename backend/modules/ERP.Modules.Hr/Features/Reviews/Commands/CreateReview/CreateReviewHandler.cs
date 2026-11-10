using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateReviewHandler(IPerformanceReviewRepository reviews, IReadRepository<Employee> employees, ReviewRules rules) : ICommandHandler<CreateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(CreateReviewCommand command, CancellationToken ct)
    {
        // 1. A known employee, an open cycle, and no review of them in it yet.
        var i = command.Input;
        var employee = await employees.FindAsync(i.EmployeeId, ct) ?? throw new FieldValidationException("employeeId", "Employee not found.");
        await rules.RequireOpenCycleAsync(i.CycleId, ct);
        if (await reviews.AnyAsync(r => r.EmployeeId == i.EmployeeId && r.CycleId == i.CycleId, ct)) throw new ConflictException("This employee already has a review in that cycle.");

        // 2. Add it with its competencies, and save.
        var review = new PerformanceReview { LegalEntityId = employee.LegalEntityId, EmployeeId = i.EmployeeId, CycleId = i.CycleId };
        ReviewRules.Map(i, review);
        review.Competencies.AddRange(ReviewRules.Competencies(review, i));
        reviews.Add(review);
        await reviews.SaveChangesAsync(ct);
        return await rules.ToDtoAsync(review, ct);
    }
}
