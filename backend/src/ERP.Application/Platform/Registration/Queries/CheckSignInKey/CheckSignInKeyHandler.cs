using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class CheckSignInKeyHandler(SignupWorkflow workflow) : IQueryHandler<CheckSignInKeyQuery, SignInKeyAvailability>
{
    public async Task<SignInKeyAvailability> Handle(CheckSignInKeyQuery query, CancellationToken ct)
    {
        var key = (query.Key ?? "").Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z0-9][a-z0-9-]{1,48}$")) return new(false, "Use 2-49 letters, digits or dashes.");
        return await workflow.KeyTakenAsync(key, null, ct) ? new(false, "That code is taken. Try another.") : new(true, null);
    }
}
