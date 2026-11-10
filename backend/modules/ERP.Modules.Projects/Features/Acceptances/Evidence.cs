using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>The evidence hash of a sign-off; no I/O. Anyone can recompute it from the stored values to check nothing was changed afterwards.</summary>
public static class Evidence
{
    public static string Hash(ProjectAcceptance a)
    {
        var image = a.SignatureImage is null ? "" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(a.SignatureImage)));
        var text = string.Join("|", a.Id, a.ProjectId, a.MilestoneId, a.Title, a.Status, a.SignedByName, a.Signature, a.Comment, a.SignedAt?.ToString("O"), a.SignedIp, image);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }
}
