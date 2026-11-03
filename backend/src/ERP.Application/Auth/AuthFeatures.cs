using ERP.Application.Abstractions;
using ERP.Application.Common;
using FluentValidation;
using MediatR;

namespace ERP.Application.Auth;

// Cookies, headers and the client IP are HTTP concerns: the endpoint reads them and passes plain values in.
public sealed record Login(string Email, string Password, bool RememberMe, string? Ip, string? TwoFactorCode = null) : ICommand<LoginResult>;
public sealed record SelectWorkspace(string SelectionToken, Guid WorkspaceId, string? Ip) : ICommand<SessionResult>;
public sealed record SwitchWorkspace(Guid WorkspaceId, bool Persistent, string? CurrentJti, DateTime? CurrentExp, string? Ip) : ICommand<SessionResult>;
public sealed record RefreshSession(string? RefreshToken, string? Ip) : ICommand<SessionResult>;
public sealed record Logout(string? RefreshToken, string? Jti, DateTime? Exp) : ICommand;
public sealed record ForgotPassword(string Email) : ICommand;
public sealed record ResetPassword(string Token, string NewPassword) : ICommand;
public sealed record ChangePassword(string CurrentPassword, string NewPassword) : ICommand;
public sealed record GetTwoFactorStatus : IQuery<TwoFactorStatus>;
/// <summary>Creates (or replaces, until enabled) the authenticator secret and returns it with the otpauth:// URI for the authenticator app.</summary>
public sealed record BeginTwoFactorSetup : ICommand<TwoFactorSetup>;
/// <summary>Confirms the first code from the app, turns two-factor on and returns the one-time recovery codes (shown once).</summary>
public sealed record EnableTwoFactor(string Code) : ICommand<TwoFactorEnabled>;
public sealed record DisableTwoFactor(string Password, string Code) : ICommand;
/// <summary>First sign-in without an authenticator: creates the key for the challenge from login and returns the QR / manual key.</summary>
public sealed record BeginTwoFactorChallengeSetup(string ChallengeToken) : ICommand<TwoFactorChallengeSetup>;
/// <summary>Confirms a login challenge (setup or returning user) with a code and issues the session.</summary>
public sealed record VerifyTwoFactorChallenge(string ChallengeToken, string Code, string? Ip) : ICommand<TwoFactorChallengeResult>;

public sealed class BeginTwoFactorChallengeSetupValidator : AbstractValidator<BeginTwoFactorChallengeSetup>
{
    public BeginTwoFactorChallengeSetupValidator() => RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(4000);
}

public sealed class VerifyTwoFactorChallengeValidator : AbstractValidator<VerifyTwoFactorChallenge>
{
    public VerifyTwoFactorChallengeValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

public sealed class LoginValidator : AbstractValidator<Login>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email and password are required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Email and password are required.");
    }
}

internal sealed class AuthHandlers(IAuthService auth, ICurrentUser user) :
    IRequestHandler<Login, LoginResult>,
    IRequestHandler<SelectWorkspace, SessionResult>,
    IRequestHandler<SwitchWorkspace, SessionResult>,
    IRequestHandler<RefreshSession, SessionResult>,
    IRequestHandler<Logout, Unit>,
    IRequestHandler<ForgotPassword, Unit>,
    IRequestHandler<ResetPassword, Unit>,
    IRequestHandler<ChangePassword, Unit>,
    IRequestHandler<GetTwoFactorStatus, TwoFactorStatus>,
    IRequestHandler<BeginTwoFactorSetup, TwoFactorSetup>,
    IRequestHandler<EnableTwoFactor, TwoFactorEnabled>,
    IRequestHandler<DisableTwoFactor, Unit>,
    IRequestHandler<BeginTwoFactorChallengeSetup, TwoFactorChallengeSetup>,
    IRequestHandler<VerifyTwoFactorChallenge, TwoFactorChallengeResult>
{
    public Task<LoginResult> Handle(Login c, CancellationToken ct) => auth.LoginAsync(c.Email, c.Password, c.RememberMe, c.Ip, c.TwoFactorCode, ct);

    public Task<SessionResult> Handle(SelectWorkspace c, CancellationToken ct) => auth.SelectWorkspaceAsync(c.SelectionToken, c.WorkspaceId, c.Ip, ct);

    public Task<SessionResult> Handle(SwitchWorkspace c, CancellationToken ct)
        => auth.SwitchWorkspaceAsync(CurrentUserId(), user.OrgId ?? throw new ForbiddenException(), c.WorkspaceId, c.CurrentJti, c.CurrentExp, c.Persistent, c.Ip, ct);

    public Task<SessionResult> Handle(RefreshSession c, CancellationToken ct) => auth.RefreshAsync(c.RefreshToken, c.Ip, ct);

    public async Task<Unit> Handle(Logout c, CancellationToken ct)
    {
        await auth.LogoutAsync(c.RefreshToken, c.Jti, c.Exp, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ForgotPassword c, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(c.Email)) await auth.ForgotPasswordAsync(c.Email, ct); // never reveals whether the email has an account
        return Unit.Value;
    }

    public async Task<Unit> Handle(ResetPassword c, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(c.Token, c.NewPassword, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ChangePassword c, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(CurrentUserId(), c.CurrentPassword, c.NewPassword, ct);
        return Unit.Value;
    }

    public Task<TwoFactorStatus> Handle(GetTwoFactorStatus c, CancellationToken ct) => auth.TwoFactorStatusAsync(CurrentUserId(), ct);
    public Task<TwoFactorSetup> Handle(BeginTwoFactorSetup c, CancellationToken ct) => auth.BeginTwoFactorSetupAsync(CurrentUserId(), ct);
    public async Task<TwoFactorEnabled> Handle(EnableTwoFactor c, CancellationToken ct) => new(await auth.EnableTwoFactorAsync(CurrentUserId(), c.Code, ct));
    public async Task<Unit> Handle(DisableTwoFactor c, CancellationToken ct) { await auth.DisableTwoFactorAsync(CurrentUserId(), c.Password, c.Code, ct); return Unit.Value; }

    public Task<TwoFactorChallengeSetup> Handle(BeginTwoFactorChallengeSetup c, CancellationToken ct) => auth.BeginChallengeSetupAsync(c.ChallengeToken, ct);
    public Task<TwoFactorChallengeResult> Handle(VerifyTwoFactorChallenge c, CancellationToken ct) => auth.VerifyChallengeAsync(c.ChallengeToken, c.Code, c.Ip, ct);

    private Guid CurrentUserId() => user.UserId ?? throw new ForbiddenException();
}
