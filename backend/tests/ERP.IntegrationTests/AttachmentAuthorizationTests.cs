using System.Net;
using System.Net.Http.Json;
using System.Text;
using ERP.Application.Security;
using Xunit;

namespace ERP.IntegrationTests;

public class AttachmentAuthorizationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static MultipartFormDataContent CreateForm(string ownerType, Guid ownerId, string fileName, string content, string declaredContentType)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(ownerType), "ownerType" },
            { new StringContent(ownerId.ToString()), "ownerId" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(content)) { Headers = { ContentType = new(declaredContentType) } }, "file", fileName }
        };
        return form;
    }

    [Fact]
    public async Task User_without_owner_read_permission_gets_403_on_list_and_download()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);

        var partyRes = await admin.PostAsJsonAsync("/api/v1/parties", new { name = "Secure Party Co", kind = "Company", roles = "Customer" });
        partyRes.EnsureSuccessStatusCode();
        var partyId = Guid.Parse((string)(await ApiFactory.Json(partyRes))["id"]!);

        var uploadRes = await admin.PostAsync("/api/v1/attachments", CreateForm("party", partyId, "contract.pdf", "sample pdf content", "application/pdf"));
        Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);
        var attachmentId = (string)(await ApiFactory.Json(uploadRes))["id"]!;

        var roleName = "AttachmentsOnly_" + Guid.NewGuid().ToString("N")[..6];
        var roleRes = await admin.PostAsJsonAsync("/api/v1/roles", new RoleInput(roleName, "Attachments only", [CorePermissions.AttachmentsRead, CorePermissions.AttachmentsWrite]));
        roleRes.EnsureSuccessStatusCode();
        var roleId = Guid.Parse((string)(await ApiFactory.Json(roleRes))["id"]!);

        var userEmail = $"restricted_{Guid.NewGuid():N}@demo.local";
        var userRes = await admin.PostAsJsonAsync("/api/v1/users", new { email = userEmail, fullName = "Restricted User", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { roleId } });
        userRes.EnsureSuccessStatusCode();
        var (restrictedClient, _) = await f.SignInAsync(userEmail, "Temp#Passw0rd");

        var listRes = await restrictedClient.GetAsync($"/api/v1/attachments?ownerType=party&ownerId={partyId}");
        Assert.Equal(HttpStatusCode.Forbidden, listRes.StatusCode);

        var downloadRes = await restrictedClient.GetAsync($"/api/v1/attachments/{attachmentId}/download");
        Assert.Equal(HttpStatusCode.Forbidden, downloadRes.StatusCode);
    }

    [Fact]
    public async Task Upload_with_unknown_owner_type_returns_400()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var res = await admin.PostAsync("/api/v1/attachments", CreateForm("unknown_entity_type", Guid.NewGuid(), "doc.pdf", "data", "application/pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_with_non_existent_owner_id_returns_404()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var res = await admin.PostAsync("/api/v1/attachments", CreateForm("party", Guid.NewGuid(), "doc.pdf", "data", "application/pdf"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Served_content_type_matches_extension_ignoring_uploaded_header()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);

        var partyRes = await admin.PostAsJsonAsync("/api/v1/parties", new { name = "Mime Test Party", kind = "Company", roles = "Customer" });
        var partyId = Guid.Parse((string)(await ApiFactory.Json(partyRes))["id"]!);

        var uploadRes = await admin.PostAsync("/api/v1/attachments", CreateForm("party", partyId, "invoice.pdf", "%PDF-1.4", "text/plain"));
        Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);
        var attachmentId = (string)(await ApiFactory.Json(uploadRes))["id"]!;

        var downloadRes = await admin.GetAsync($"/api/v1/attachments/{attachmentId}/download");
        Assert.Equal(HttpStatusCode.OK, downloadRes.StatusCode);
        Assert.Equal("application/pdf", downloadRes.Content.Headers.ContentType?.MediaType);
    }
}
