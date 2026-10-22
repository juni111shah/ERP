namespace ERP.Application.Security;

/// <summary>Claim names shared by the token issuer (Infrastructure) and the HTTP pipeline (Api).</summary>
public static class ErpClaims
{
    public const string Org = "org";
    public const string Permission = "perm";
    public const string Entities = "ents";
    public const string AllEntities = "*";
}
