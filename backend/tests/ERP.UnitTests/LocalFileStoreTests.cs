using ERP.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ERP.UnitTests;

public class LocalFileStoreTests
{
    [Fact]
    public async Task PathFor_rejects_sibling_directory_traversal()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "erp_filestore_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:Root"] = tempDir
                })
                .Build();

            var store = new LocalFileStore(config);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.OpenAsync(@"..\storage2\malicious.txt"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.DeleteAsync(@"../storage2/malicious.txt"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
