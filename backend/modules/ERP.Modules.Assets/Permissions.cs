namespace ERP.Modules.Assets;

public static class Permissions
{
    public const string CategoriesRead = "assets.categories.read";
    public const string CategoriesManage = "assets.categories.manage";
    public const string RegisterRead = "assets.register.read";
    public const string RegisterManage = "assets.register.manage";
    public const string RegisterCapitalise = "assets.register.capitalise";
    public const string RegisterDispose = "assets.register.dispose";
    public const string DepreciationRead = "assets.depreciation.read";
    public const string DepreciationRun = "assets.depreciation.run";
    public const string ReportsRead = "assets.reports.read";

    public static readonly string[] All =
    [
        CategoriesRead, CategoriesManage, RegisterRead, RegisterManage, RegisterCapitalise, RegisterDispose, DepreciationRead, DepreciationRun, ReportsRead,
    ];
}
