using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

// ---- Tax codes ----------------------------------------------------------------------------------------------------

public sealed record TaxCodeInput(string Code, string Name, TaxKind Kind, decimal Rate, bool IsRecoverable = true, bool IsDefault = false, bool IsActive = true);

public sealed record CreateTaxCode(TaxCodeInput Input) : ICommand<TaxCodeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.TaxWrite;
}

public sealed record UpdateTaxCode(Guid Id, TaxCodeInput Input) : ICommand<TaxCodeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.TaxWrite;
}

public sealed class TaxCodeInputValidator : AbstractValidator<TaxCodeInput>
{
    public TaxCodeInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Rate).InclusiveBetween(0, 100).WithMessage("The rate is a percentage between 0 and 100.");
        RuleFor(x => x).Must(x => !x.IsDefault || x.IsActive).WithName("isDefault").WithMessage("The default tax code must be active.");
    }
}

public sealed class CreateTaxCodeValidator : AbstractValidator<CreateTaxCode>
{
    public CreateTaxCodeValidator() => RuleFor(x => x.Input).SetValidator(new TaxCodeInputValidator());
}

public sealed class UpdateTaxCodeValidator : AbstractValidator<UpdateTaxCode>
{
    public UpdateTaxCodeValidator() => RuleFor(x => x.Input).SetValidator(new TaxCodeInputValidator());
}

// ---- Chart of accounts --------------------------------------------------------------------------------------------

public sealed record AccountInput(
    string Code, string Name, AccountType Type, string? ReportingGroup = null, Guid? ParentId = null, bool IsPostable = true, bool IsBank = false,
    bool IsActive = true, string? CurrencyCode = null, JsonObject? CustomFields = null);

public sealed record CreateAccount(AccountInput Input) : ICommand<AccountDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsWrite;
}

/// <summary>System accounts (those posting rules rely on) keep their code, type and active state; only the descriptive fields change.</summary>
public sealed record UpdateAccount(Guid Id, AccountInput Input) : ICommand<AccountDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsWrite;
}

public sealed class AccountInputValidator : AbstractValidator<AccountInput>
{
    public AccountInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.CurrencyCode).Length(3).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
    }
}

public sealed class CreateAccountValidator : AbstractValidator<CreateAccount>
{
    public CreateAccountValidator() => RuleFor(x => x.Input).SetValidator(new AccountInputValidator());
}

public sealed class UpdateAccountValidator : AbstractValidator<UpdateAccount>
{
    public UpdateAccountValidator() => RuleFor(x => x.Input).SetValidator(new AccountInputValidator());
}

internal sealed class FinanceHandlers(IRepository<TaxCode> taxCodes, IRepository<Account> accounts, ICustomFieldService fields) :
    IRequestHandler<CreateTaxCode, TaxCodeDto>,
    IRequestHandler<UpdateTaxCode, TaxCodeDto>,
    IRequestHandler<CreateAccount, AccountDto>,
    IRequestHandler<UpdateAccount, AccountDto>
{
    private const string AccountType = "account";

    public async Task<TaxCodeDto> Handle(CreateTaxCode cmd, CancellationToken ct)
    {
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (await taxCodes.AnyAsync(t => t.Code == code && t.LegalEntityId == null, ct)) throw new ConflictException($"A tax code '{code}' exists.");
        var tax = new TaxCode { Code = code };
        await ApplyAsync(tax, cmd.Input, ct);
        taxCodes.Add(tax);
        await taxCodes.SaveChangesAsync(ct);
        return ToDto(tax);
    }

    public async Task<TaxCodeDto> Handle(UpdateTaxCode cmd, CancellationToken ct)
    {
        var tax = await taxCodes.GetForUpdateAsync(cmd.Id, ct);
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (code != tax.Code && await taxCodes.AnyAsync(t => t.Code == code && t.LegalEntityId == tax.LegalEntityId, ct)) throw new ConflictException($"A tax code '{code}' exists.");
        tax.Code = code;
        await ApplyAsync(tax, cmd.Input, ct);
        await taxCodes.SaveChangesAsync(ct);
        return ToDto(tax);
    }

    public async Task<AccountDto> Handle(CreateAccount cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var code = i.Code.Trim();
        if (await accounts.AnyAsync(a => a.Code == code, ct)) throw new ConflictException($"An account with code '{code}' exists.");
        await EnsureParentAsync(i.ParentId, null, ct);
        var account = new Account { Code = code, Type = i.Type };
        Map(i, account);
        await fields.ApplyAsync(AccountType, account, i.CustomFields, isCreate: true, ct);
        accounts.Add(account);
        await accounts.SaveChangesAsync(ct);
        return AccountDto.From(account, await fields.ReadAsync(AccountType, account, ct));
    }

    public async Task<AccountDto> Handle(UpdateAccount cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var account = await accounts.GetForUpdateAsync(cmd.Id, ct);
        var code = i.Code.Trim();
        if (account.IsSystem)
        {
            if (code != account.Code || i.Type != account.Type) throw new ConflictException("The code and type of a system account cannot change (posting rules depend on them).");
            if (!i.IsActive) throw new ConflictException("A system account cannot be deactivated.");
        }
        else
        {
            if (code != account.Code && await accounts.AnyAsync(a => a.Code == code, ct)) throw new ConflictException($"An account with code '{code}' exists.");
            account.Code = code; account.Type = i.Type;
        }
        await EnsureParentAsync(i.ParentId, account.Id, ct);
        Map(i, account);
        await fields.ApplyAsync(AccountType, account, i.CustomFields, isCreate: false, ct);
        await accounts.SaveChangesAsync(ct);
        return AccountDto.From(account, await fields.ReadAsync(AccountType, account, ct));
    }

    /// <summary>Applies the input; making a code the default clears the default flag on every other tax code.</summary>
    private async Task ApplyAsync(TaxCode tax, TaxCodeInput i, CancellationToken ct)
    {
        tax.Name = i.Name.Trim(); tax.Kind = i.Kind; tax.Rate = i.Rate; tax.IsRecoverable = i.IsRecoverable; tax.IsActive = i.IsActive;
        if (i.IsDefault && !tax.IsDefault)
            foreach (var other in await taxCodes.Tracked(new Specification<TaxCode>(t => t.IsDefault && t.Id != tax.Id)).ToListAsync(ct)) other.IsDefault = false;
        tax.IsDefault = i.IsDefault;
    }

    private async Task EnsureParentAsync(Guid? parentId, Guid? selfId, CancellationToken ct)
    {
        if (parentId is not { } parent) return;
        if (parent == selfId) throw new FieldValidationException("parentId", "An account cannot be its own parent.");
        if (!await accounts.AnyAsync(a => a.Id == parent, ct)) throw new FieldValidationException("parentId", "Parent account not found.");
        // Walk up the tree so a parent can never be one of the account's own descendants.
        for (Guid? current = parent; current is not null && selfId is not null;)
        {
            var next = await accounts.Query(new Specification<Account>(a => a.Id == current)).Select(a => a.ParentId).FirstOrDefaultAsync(ct);
            if (next == selfId) throw new FieldValidationException("parentId", "That parent would create a loop in the chart of accounts.");
            current = next;
        }
    }

    private static void Map(AccountInput i, Account a)
    {
        a.Name = i.Name.Trim(); a.ReportingGroup = i.ReportingGroup; a.ParentId = i.ParentId; a.IsPostable = i.IsPostable; a.IsBank = i.IsBank;
        a.IsActive = i.IsActive; a.CurrencyCode = i.CurrencyCode?.ToUpperInvariant();
    }

    private static TaxCodeDto ToDto(TaxCode t) => new(t.Id, t.Code, t.Name, t.Kind, t.Rate, t.IsDefault, t.IsRecoverable, t.IsActive);
}
