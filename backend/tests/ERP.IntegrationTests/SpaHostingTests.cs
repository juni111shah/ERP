using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;

namespace ERP.IntegrationTests;

/// <summary>A published package serves the compiled web app from wwwroot, on the same origin as the API.</summary>
public class SpaHostingTests(SpaHostingTests.SpaFactory f) : IClassFixture<SpaHostingTests.SpaFactory>
{
    private const string AppMarker = "<erp-root>";

    /// <summary>The API host with a small wwwroot. Without one the host never turns SPA serving on, so no other test covers it.</summary>
    public sealed class SpaFactory : ApiFactory
    {
        private readonly string _webRoot = Directory.CreateTempSubdirectory("erp-spa-tests-").FullName;

        public SpaFactory()
        {
            Write("index.html", $"<!doctype html><html><body>{AppMarker}</erp-root></body></html>");
            Write("main-ABC123.js", "console.log('main');");
            Write("chunk-DEF456.js", "export const chunk = 1;");
            Write("styles-ABC123.css", "body { margin: 0; }");
            Write("favicon.ico", "not-really-an-icon");
            Write("assets/logo.png", "not-really-a-png");
            Write("assets/logo.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        }

        public string Read(string relativePath) => File.ReadAllText(Path.Combine(_webRoot, relativePath));

        private void Write(string relativePath, string content)
        {
            var path = Path.Combine(_webRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseWebRoot(_webRoot);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("main-ABC123.js", "text/javascript")]
    [InlineData("chunk-DEF456.js", "text/javascript")]
    [InlineData("styles-ABC123.css", "text/css")]
    [InlineData("favicon.ico", "image/x-icon")]
    [InlineData("assets/logo.png", "image/png")]
    [InlineData("assets/logo.svg", "image/svg+xml")]
    public async Task Static_files_are_served_with_their_own_content_type(string file, string contentType)
    {
        var res = await f.Client().GetAsync("/" + file);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(contentType, res.Content.Headers.ContentType?.MediaType);
        Assert.Equal(f.Read(file), await res.Content.ReadAsStringAsync());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/pricing")]
    [InlineData("/auth/login")]
    [InlineData("/some/deep/client/route")]
    public async Task App_routes_return_the_app_without_signing_in(string path)
    {
        var res = await f.Client().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/html", res.Content.Headers.ContentType?.MediaType);
        Assert.Contains(AppMarker, await res.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/v1/does-not-exist")]
    [InlineData("/api/v1/does-not-exist.js")]
    [InlineData("/API/V1/DOES-NOT-EXIST")]
    [InlineData("/api/v9/anything")]
    public async Task Unknown_api_paths_return_a_json_404_when_signed_in(string path)
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);

        var res = await c.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.EndsWith("json", res.Content.Headers.ContentType?.MediaType);
        var env = await ApiFactory.Envelope(res);
        Assert.False((bool)env["success"]!);
        Assert.Equal(404, (int)env["statusCode"]!);
    }

    [Theory]
    [InlineData("/api/v1/does-not-exist")]
    [InlineData("/api/v1/me")]
    public async Task Api_paths_return_a_json_401_when_anonymous(string path)
    {
        var res = await f.Client().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.EndsWith("json", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, (int)(await ApiFactory.Envelope(res))["statusCode"]!);
    }

    [Fact]
    public async Task Known_api_endpoints_still_work_alongside_the_app()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);

        var res = await c.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((bool)(await ApiFactory.Envelope(res))["success"]!);
    }

    [Theory]
    [InlineData("/hubs/does-not-exist")]
    [InlineData("/swagger/does-not-exist")]
    [InlineData("/health")]
    public async Task Server_paths_never_return_the_app(string path)
    {
        var res = await f.Client().GetAsync(path);

        Assert.DoesNotContain(AppMarker, await res.Content.ReadAsStringAsync());
    }
}
