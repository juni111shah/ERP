using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record StatutorySlabInput(decimal FromAmount, decimal Rate);
public sealed record StatutoryRuleInput(Guid LegalEntityId, string Code, string Name, StatutoryKind Kind, StatutoryMethod Method, StatutoryBase Base, decimal Rate = 0, decimal Threshold = 0, decimal Cap = 0,
    string? Nationalities = null, DateOnly? EffectiveFrom = null, DateOnly? EffectiveTo = null, bool IsActive = true, IReadOnlyList<StatutorySlabInput>? Slabs = null);
public sealed record StatutoryRuleDto(Guid Id, Guid LegalEntityId, string Code, string Name, StatutoryKind Kind, StatutoryMethod Method, StatutoryBase Base, decimal Rate, decimal Threshold, decimal Cap,
    string? Nationalities, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool IsActive, IReadOnlyList<StatutorySlabInput> Slabs);
public sealed record StatutoryRuleRow(Guid Id, Guid LegalEntityId, string Code, string Name, StatutoryKind Kind, StatutoryMethod Method, StatutoryBase Base, decimal Rate, bool IsActive);
public sealed record StatutoryTotalDto(string Code, string Name, StatutoryKind Kind, int Employees, decimal BaseAmount, decimal Amount);

public sealed record GetStatutoryRule(Guid Id) : IQuery<StatutoryRuleDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
public sealed record CreateStatutoryRule(StatutoryRuleInput Input) : ICommand<StatutoryRuleDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
public sealed record UpdateStatutoryRule(Guid Id, StatutoryRuleInput Input) : ICommand<StatutoryRuleDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
public sealed record DeleteStatutoryRule(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
/// <summary>Per rule: how many employees, the base and the amount of a payroll run (what is filed with the authority).</summary>
public sealed record GetPayrollStatutory(Guid RunId) : IQuery<IReadOnlyList<StatutoryTotalDto>>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }

public sealed class StatutoryRuleInputValidator : AbstractValidator<StatutoryRuleInput>
{
    public StatutoryRuleInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Kind).IsInEnum(); RuleFor(x => x.Method).IsInEnum(); RuleFor(x => x.Base).IsInEnum();
        RuleFor(x => x.Rate).InclusiveBetween(0, 100);
        RuleFor(x => x.Threshold).GreaterThanOrEqualTo(0); RuleFor(x => x.Cap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Nationalities).MaximumLength(300);
        RuleFor(x => x.EffectiveTo!.Value).GreaterThanOrEqualTo(x => x.EffectiveFrom!.Value).When(x => x.EffectiveFrom is not null && x.EffectiveTo is not null).WithName("EffectiveTo");
        RuleForEach(x => x.Slabs).ChildRules(s => { s.RuleFor(y => y.FromAmount).GreaterThanOrEqualTo(0); s.RuleFor(y => y.Rate).InclusiveBetween(0, 100); });
        RuleFor(x => x.Slabs).Must(s => s is { Count: > 0 }).When(x => x.Method == StatutoryMethod.Slab).WithMessage("A slab rule needs at least one slab.");
        RuleFor(x => x.Slabs).Must(s => s is null || s.Select(y => y.FromAmount).Distinct().Count() == s.Count).WithMessage("Two slabs cannot start at the same amount.");
    }
}
public sealed class CreateStatutoryRuleValidator : AbstractValidator<CreateStatutoryRule> { public CreateStatutoryRuleValidator() => RuleFor(x => x.Input).SetValidator(new StatutoryRuleInputValidator()); }
public sealed class UpdateStatutoryRuleValidator : AbstractValidator<UpdateStatutoryRule> { public UpdateStatutoryRuleValidator() => RuleFor(x => x.Input).SetValidator(new StatutoryRuleInputValidator()); }

/// <summary>The arithmetic of one rule; no I/O.</summary>
public static class StatutoryMath
{
    public static decimal BaseFor(StatutoryRule r, decimal basic, decimal gross)
    {
        var b = r.Base == StatutoryBase.Basic ? basic : gross;
        if (r.Cap > 0 && b > r.Cap) b = r.Cap;
        return b < 0 ? 0 : b;
    }

    public static decimal Amount(StatutoryRule r, decimal baseAmount)
    {
        if (baseAmount <= 0 || (r.Threshold > 0 && baseAmount < r.Threshold)) return 0m;
        if (r.Method == StatutoryMethod.Flat) return PayrollMath.R(baseAmount * r.Rate / 100m);
        var slabs = r.Slabs.OrderBy(s => s.FromAmount).ToList();
        decimal total = 0;
        for (var i = 0; i < slabs.Count; i++)
        {
            var upper = i + 1 < slabs.Count ? slabs[i + 1].FromAmount : decimal.MaxValue;
            var part = Math.Min(baseAmount, upper) - slabs[i].FromAmount;
            if (part > 0) total += part * slabs[i].Rate / 100m;
        }
        return PayrollMath.R(total);
    }

    /// <summary>Does the rule apply to this nationality? Blank = everybody; "!Qatari" = everybody but Qataris; "Qatari, Omani" = only them.</summary>
    public static bool AppliesTo(string? nationalities, string? nationality)
    {
        if (string.IsNullOrWhiteSpace(nationalities)) return true;
        var parts = nationalities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var excluded = parts.Where(p => p.StartsWith('!')).Select(p => p[1..].Trim()).ToList();
        var included = parts.Where(p => !p.StartsWith('!')).ToList();
        var n = (nationality ?? "").Trim();
        if (excluded.Any(e => e.Equals(n, StringComparison.OrdinalIgnoreCase))) return false;
        return included.Count == 0 || included.Any(i => i.Equals(n, StringComparison.OrdinalIgnoreCase));
    }

    public static bool InForce(StatutoryRule r, DateOnly monthStart, DateOnly monthEnd)
        => r.IsActive && (r.EffectiveFrom is null || r.EffectiveFrom <= monthEnd) && (r.EffectiveTo is null || r.EffectiveTo >= monthStart);
}

public sealed record StatutoryLineResult(Guid RuleId, string Code, string Name, StatutoryKind Kind, decimal BaseAmount, decimal Amount);

/// <summary>Loads an entity's statutory rules for a month and works out what applies to an employee.</summary>
public sealed class StatutoryEngine(IReadRepository<StatutoryRule> rules)
{
    public async Task<IReadOnlyList<StatutoryRule>> LoadAsync(Guid legalEntityId, int year, int month, CancellationToken ct)
    {
        var start = new DateOnly(year, month, 1); var end = start.AddMonths(1).AddDays(-1);
        var all = await rules.Query(new Specification<StatutoryRule>(r => r.LegalEntityId == legalEntityId && r.IsActive).Include(r => r.Slabs)).ToListAsync(ct);
        return all.Where(r => StatutoryMath.InForce(r, start, end)).OrderBy(r => r.Code).ToList();
    }

    public static IReadOnlyList<StatutoryLineResult> For(IReadOnlyList<StatutoryRule> rules, string? nationality, decimal basic, decimal gross)
        => rules.Where(r => StatutoryMath.AppliesTo(r.Nationalities, nationality))
            .Select(r => { var b = StatutoryMath.BaseFor(r, basic, gross); return new StatutoryLineResult(r.Id, r.Code, r.Name, r.Kind, b, StatutoryMath.Amount(r, b)); })
            .Where(x => x.Amount > 0).ToList();
}

internal sealed class StatutoryHandlers(IRepository<StatutoryRule> rules, IRepository<StatutorySlab> slabs, IReadRepository<PayrollLineStatutory> lines, ILegalEntityGuard guard) :
    IRequestHandler<GetStatutoryRule, StatutoryRuleDto>,
    IRequestHandler<CreateStatutoryRule, StatutoryRuleDto>,
    IRequestHandler<UpdateStatutoryRule, StatutoryRuleDto>,
    IRequestHandler<DeleteStatutoryRule, Unit>,
    IRequestHandler<GetPayrollStatutory, IReadOnlyList<StatutoryTotalDto>>
{
    public async Task<StatutoryRuleDto> Handle(GetStatutoryRule q, CancellationToken ct)
        => ToDto(await rules.Query(new Specification<StatutoryRule>(r => r.Id == q.Id).Include(r => r.Slabs)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException());

    public async Task<StatutoryRuleDto> Handle(CreateStatutoryRule cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var code = i.Code.Trim().ToUpperInvariant();
        if (await rules.AnyAsync(r => r.LegalEntityId == entity.Id && r.Code == code, ct)) throw new ConflictException($"A rule with code '{code}' already exists for this entity.");
        var rule = new StatutoryRule { LegalEntityId = entity.Id };
        Fill(rule, i, code);
        rules.Add(rule);
        AddSlabs(rule, i);
        await rules.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<StatutoryRuleDto> Handle(UpdateStatutoryRule cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var rule = await rules.Tracked(new Specification<StatutoryRule>(r => r.Id == cmd.Id).Include(r => r.Slabs)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (i.LegalEntityId != rule.LegalEntityId) throw new FieldValidationException("legalEntityId", "A rule cannot move to another entity.");
        var code = i.Code.Trim().ToUpperInvariant();
        if (code != rule.Code && await rules.AnyAsync(r => r.LegalEntityId == rule.LegalEntityId && r.Code == code && r.Id != rule.Id, ct)) throw new ConflictException($"A rule with code '{code}' already exists for this entity.");
        Fill(rule, i, code);
        foreach (var old in rule.Slabs.ToList()) slabs.Remove(old);
        rule.Slabs.Clear();
        AddSlabs(rule, i);
        await rules.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<Unit> Handle(DeleteStatutoryRule cmd, CancellationToken ct)
    {
        var rule = await rules.Tracked(new Specification<StatutoryRule>(r => r.Id == cmd.Id).Include(r => r.Slabs)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (await lines.AnyAsync(l => l.RuleId == rule.Id, ct)) throw new ConflictException("This rule was used in payroll. Switch it off instead of deleting it.");
        foreach (var s in rule.Slabs.ToList()) slabs.Remove(s);
        rules.Remove(rule);
        await rules.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<IReadOnlyList<StatutoryTotalDto>> Handle(GetPayrollStatutory q, CancellationToken ct)
        => (await lines.Query(new Specification<PayrollLineStatutory>(l => l.PayrollRunId == q.RunId)).ToListAsync(ct))
            .GroupBy(l => (l.Code, l.Name, l.Kind)).OrderBy(g => g.Key.Code)
            .Select(g => new StatutoryTotalDto(g.Key.Code, g.Key.Name, g.Key.Kind, g.Select(x => x.EmployeeId).Distinct().Count(), g.Sum(x => x.BaseAmount), g.Sum(x => x.Amount))).ToList();

    private static void Fill(StatutoryRule r, StatutoryRuleInput i, string code)
    {
        r.Code = code; r.Name = i.Name.Trim(); r.Kind = i.Kind; r.Method = i.Method; r.Base = i.Base; r.Rate = i.Method == StatutoryMethod.Flat ? i.Rate : 0m;
        r.Threshold = i.Threshold; r.Cap = i.Cap; r.Nationalities = string.IsNullOrWhiteSpace(i.Nationalities) ? null : i.Nationalities.Trim();
        r.EffectiveFrom = i.EffectiveFrom; r.EffectiveTo = i.EffectiveTo; r.IsActive = i.IsActive;
    }

    private void AddSlabs(StatutoryRule rule, StatutoryRuleInput i)
    {
        if (i.Method != StatutoryMethod.Slab) return;
        foreach (var s in i.Slabs ?? []) { var slab = new StatutorySlab { RuleId = rule.Id, FromAmount = s.FromAmount, Rate = s.Rate }; slabs.Add(slab); rule.Slabs.Add(slab); }
    }

    internal static StatutoryRuleDto ToDto(StatutoryRule r) => new(r.Id, r.LegalEntityId, r.Code, r.Name, r.Kind, r.Method, r.Base, r.Rate, r.Threshold, r.Cap, r.Nationalities, r.EffectiveFrom, r.EffectiveTo, r.IsActive,
        r.Slabs.OrderBy(s => s.FromAmount).Select(s => new StatutorySlabInput(s.FromAmount, s.Rate)).ToList());
}
