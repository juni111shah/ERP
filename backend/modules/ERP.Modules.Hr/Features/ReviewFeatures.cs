using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record ReviewCycleInput(string Name, DateOnly PeriodStart, DateOnly PeriodEnd, ReviewCycleStatus Status = ReviewCycleStatus.Open);
public sealed record ReviewCycleDto(Guid Id, string Name, DateOnly PeriodStart, DateOnly PeriodEnd, ReviewCycleStatus Status);

public sealed record CompetencyInput(string Name, int Score, string? Notes = null);
public sealed record ReviewInput(Guid EmployeeId, Guid CycleId, string? ReviewerName, int? OverallRating, string? Goals, string? Comments, IReadOnlyList<CompetencyInput> Competencies);
public sealed record CompetencyDto(Guid Id, string Name, int Score, string? Notes);
public sealed record ReviewDto(Guid Id, Guid EmployeeId, string EmployeeName, Guid CycleId, string CycleName, string? ReviewerName, int? OverallRating, string? Goals, string? Comments, ReviewStatus Status,
    DateTime? SubmittedAt, DateTime? AcknowledgedAt, IReadOnlyList<CompetencyDto> Competencies);
public sealed record ReviewRow(Guid Id, Guid EmployeeId, Guid CycleId, string? ReviewerName, int? OverallRating, ReviewStatus Status, DateTime? SubmittedAt);

public sealed record CreateReviewCycle(ReviewCycleInput Input) : ICommand<ReviewCycleDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
public sealed record UpdateReviewCycle(Guid Id, ReviewCycleInput Input) : ICommand<ReviewCycleDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
public sealed record GetReview(Guid Id) : IQuery<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsRead; }
public sealed record CreateReview(ReviewInput Input) : ICommand<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
public sealed record UpdateReview(Guid Id, ReviewInput Input) : ICommand<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
public sealed record DeleteReview(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
/// <summary>Locks the review for the employee to read; needs an overall rating.</summary>
public sealed record SubmitReview(Guid Id) : ICommand<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
/// <summary>Records that the employee has seen and acknowledged the submitted review.</summary>
public sealed record AcknowledgeReview(Guid Id) : ICommand<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }

public sealed class ReviewCycleInputValidator : AbstractValidator<ReviewCycleInput>
{
    public ReviewCycleInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x).Must(x => x.PeriodEnd >= x.PeriodStart).WithName("periodEnd").WithMessage("The period must end on or after it starts.");
    }
}
public sealed class CreateReviewCycleValidator : AbstractValidator<CreateReviewCycle> { public CreateReviewCycleValidator() => RuleFor(x => x.Input).SetValidator(new ReviewCycleInputValidator()); }
public sealed class UpdateReviewCycleValidator : AbstractValidator<UpdateReviewCycle> { public UpdateReviewCycleValidator() => RuleFor(x => x.Input).SetValidator(new ReviewCycleInputValidator()); }

public sealed class ReviewInputValidator : AbstractValidator<ReviewInput>
{
    public ReviewInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.CycleId).NotEmpty();
        RuleFor(x => x.ReviewerName).MaximumLength(150);
        RuleFor(x => x.OverallRating).InclusiveBetween(1, 5).When(x => x.OverallRating is not null);
        RuleFor(x => x.Goals).MaximumLength(4000);
        RuleFor(x => x.Comments).MaximumLength(4000);
        RuleFor(x => x.Competencies).NotNull().Must(c => c is null || c.Count <= 30).WithMessage("At most 30 competencies.");
        RuleForEach(x => x.Competencies).ChildRules(c =>
        {
            c.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            c.RuleFor(x => x.Score).InclusiveBetween(1, 5);
            c.RuleFor(x => x.Notes).MaximumLength(1000);
        });
    }
}
public sealed class CreateReviewValidator : AbstractValidator<CreateReview> { public CreateReviewValidator() => RuleFor(x => x.Input).SetValidator(new ReviewInputValidator()); }
public sealed class UpdateReviewValidator : AbstractValidator<UpdateReview> { public UpdateReviewValidator() => RuleFor(x => x.Input).SetValidator(new ReviewInputValidator()); }

internal sealed class ReviewHandlers(IRepository<ReviewCycle> cycles, IRepository<PerformanceReview> reviews, IRepository<ReviewCompetency> competencyRows, IReadRepository<Employee> employees, IClock clock) :
    IRequestHandler<CreateReviewCycle, ReviewCycleDto>,
    IRequestHandler<UpdateReviewCycle, ReviewCycleDto>,
    IRequestHandler<GetReview, ReviewDto>,
    IRequestHandler<CreateReview, ReviewDto>,
    IRequestHandler<UpdateReview, ReviewDto>,
    IRequestHandler<DeleteReview, Unit>,
    IRequestHandler<SubmitReview, ReviewDto>,
    IRequestHandler<AcknowledgeReview, ReviewDto>
{
    public async Task<ReviewCycleDto> Handle(CreateReviewCycle cmd, CancellationToken ct)
    {
        var name = cmd.Input.Name.Trim();
        if (await cycles.AnyAsync(c => c.Name == name, ct)) throw new ConflictException($"A review cycle named '{name}' already exists.");
        var cycle = new ReviewCycle { Name = name, PeriodStart = cmd.Input.PeriodStart, PeriodEnd = cmd.Input.PeriodEnd, Status = cmd.Input.Status };
        cycles.Add(cycle);
        await cycles.SaveChangesAsync(ct);
        return Dto(cycle);
    }

    public async Task<ReviewCycleDto> Handle(UpdateReviewCycle cmd, CancellationToken ct)
    {
        var cycle = await cycles.GetForUpdateAsync(cmd.Id, ct);
        var name = cmd.Input.Name.Trim();
        if (name != cycle.Name && await cycles.AnyAsync(c => c.Name == name && c.Id != cycle.Id, ct)) throw new ConflictException($"A review cycle named '{name}' already exists.");
        cycle.Name = name; cycle.PeriodStart = cmd.Input.PeriodStart; cycle.PeriodEnd = cmd.Input.PeriodEnd; cycle.Status = cmd.Input.Status;
        await cycles.SaveChangesAsync(ct);
        return Dto(cycle);
    }

    public async Task<ReviewDto> Handle(GetReview q, CancellationToken ct) => await ToDtoAsync(await LoadAsync(q.Id, false, ct), ct);

    public async Task<ReviewDto> Handle(CreateReview cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var employee = await employees.FindAsync(i.EmployeeId, ct) ?? throw new FieldValidationException("employeeId", "Employee not found.");
        await RequireOpenCycleAsync(i.CycleId, ct);
        if (await reviews.AnyAsync(r => r.EmployeeId == i.EmployeeId && r.CycleId == i.CycleId, ct)) throw new ConflictException("This employee already has a review in that cycle.");
        var review = new PerformanceReview { LegalEntityId = employee.LegalEntityId, EmployeeId = i.EmployeeId, CycleId = i.CycleId };
        Map(i, review);
        review.Competencies.AddRange(Competencies(review, i));
        reviews.Add(review);
        await reviews.SaveChangesAsync(ct);
        return await ToDtoAsync(review, ct);
    }

    public async Task<ReviewDto> Handle(UpdateReview cmd, CancellationToken ct)
    {
        var review = await LoadAsync(cmd.Id, true, ct);
        if (review.Status != ReviewStatus.Draft) throw new ConflictException("Only a draft review can be edited.");
        var i = cmd.Input;
        if (i.EmployeeId != review.EmployeeId || i.CycleId != review.CycleId) throw new FieldValidationException("employeeId", "The employee and cycle of a review cannot change.");
        await RequireOpenCycleAsync(review.CycleId, ct);
        Map(i, review);
        // New children with client-side GUIDs go through their own repository (see journals): adding through the navigation alone is read as an update.
        foreach (var old in review.Competencies.ToList()) competencyRows.Remove(old);
        var fresh = Competencies(review, i);
        competencyRows.AddRange(fresh);
        await reviews.SaveChangesAsync(ct);
        review.Competencies.Clear();
        review.Competencies.AddRange(fresh);
        return await ToDtoAsync(review, ct);
    }

    public async Task<Unit> Handle(DeleteReview cmd, CancellationToken ct)
    {
        var review = await reviews.GetForUpdateAsync(cmd.Id, ct);
        if (review.Status != ReviewStatus.Draft) throw new ConflictException("Only a draft review can be deleted.");
        reviews.Remove(review);
        await reviews.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<ReviewDto> Handle(SubmitReview cmd, CancellationToken ct)
    {
        var review = await LoadAsync(cmd.Id, true, ct);
        if (review.Status != ReviewStatus.Draft) throw new ConflictException("This review has already been submitted.");
        if (review.OverallRating is null) throw new ConflictException("Give an overall rating before submitting.");
        review.Status = ReviewStatus.Submitted; review.SubmittedAt = clock.UtcNow;
        await reviews.SaveChangesAsync(ct);
        return await ToDtoAsync(review, ct);
    }

    public async Task<ReviewDto> Handle(AcknowledgeReview cmd, CancellationToken ct)
    {
        var review = await LoadAsync(cmd.Id, true, ct);
        if (review.Status != ReviewStatus.Submitted) throw new ConflictException(review.Status == ReviewStatus.Draft ? "Submit the review first." : "This review is already acknowledged.");
        review.Status = ReviewStatus.Acknowledged; review.AcknowledgedAt = clock.UtcNow;
        await reviews.SaveChangesAsync(ct);
        return await ToDtoAsync(review, ct);
    }

    private async Task RequireOpenCycleAsync(Guid cycleId, CancellationToken ct)
    {
        var cycle = await cycles.FindAsync(cycleId, ct) ?? throw new FieldValidationException("cycleId", "Review cycle not found.");
        if (cycle.Status != ReviewCycleStatus.Open) throw new ConflictException($"The review cycle '{cycle.Name}' is closed.");
    }

    private async Task<PerformanceReview> LoadAsync(Guid id, bool tracked, CancellationToken ct)
    {
        var spec = new Specification<PerformanceReview>(r => r.Id == id).Include(r => r.Competencies);
        return await (tracked ? reviews.Tracked(spec) : reviews.Query(spec)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
    }

    private static void Map(ReviewInput i, PerformanceReview r) { r.ReviewerName = i.ReviewerName?.Trim(); r.OverallRating = i.OverallRating; r.Goals = i.Goals; r.Comments = i.Comments; }

    private static List<ReviewCompetency> Competencies(PerformanceReview r, ReviewInput i)
        => i.Competencies.Select(c => new ReviewCompetency { ReviewId = r.Id, Name = c.Name.Trim(), Score = c.Score, Notes = c.Notes }).ToList();

    private static ReviewCycleDto Dto(ReviewCycle c) => new(c.Id, c.Name, c.PeriodStart, c.PeriodEnd, c.Status);

    private async Task<ReviewDto> ToDtoAsync(PerformanceReview r, CancellationToken ct)
    {
        var name = await employees.Query(new Specification<Employee>(e => e.Id == r.EmployeeId)).Select(e => e.FullName).FirstOrDefaultAsync(ct) ?? "";
        var cycle = await cycles.Query(new Specification<ReviewCycle>(c => c.Id == r.CycleId)).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "";
        return new(r.Id, r.EmployeeId, name, r.CycleId, cycle, r.ReviewerName, r.OverallRating, r.Goals, r.Comments, r.Status, r.SubmittedAt, r.AcknowledgedAt,
            r.Competencies.Select(c => new CompetencyDto(c.Id, c.Name, c.Score, c.Notes)).ToList());
    }
}
