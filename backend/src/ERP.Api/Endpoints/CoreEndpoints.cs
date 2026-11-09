namespace ERP.Api.Endpoints;

/// <summary>
/// Composition of the core HTTP surface. Every endpoint is a thin controller: bind the request, send one MediatR
/// command/query, shape the HTTP result. Rules live in handlers (Application); data access lives in repositories.
/// </summary>
public static class CoreEndpoints
{
    public static void MapCore(this RouteGroupBuilder api)
    {
        api.MapSetup();
        api.MapSession();
        api.MapPlatform();
        api.MapRegistration();
        api.MapBilling();
        api.MapPlatformAdmin();
        api.MapModules();
        api.MapOrganization();
        api.MapSecurity();
        api.MapMasterData();
        api.MapReceivables();
        api.MapCheques();
        api.MapBank();
        api.MapPayables();
        api.MapDashboard();
        api.MapCustomFields();
        api.MapWorkflow();
        api.MapDocumentEmail();
        api.MapImports();
        api.MapPortal();
        api.MapPrint();
    }
}
