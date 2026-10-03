using System.Net;
using System.Security.Cryptography;
using System.Text;
using IoBuild.Api.Profiles.Infrastructure.Cloudinary;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Profiles.Photo.Contract;

[Trait("Context", "Profiles")]
[Trait("Capability", "Photo")]
[Trait("Layer", "Contract")]
[Trait("Dependency", "FakeHttp")]
public sealed class CloudinaryHttpAdapterTests
{
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Cloudinary_adapter_posts_a_signed_multipart_upload_contract()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"secure_url\":\"https://res.cloudinary.com/demo/image/upload/a.png\"}");
        var uploader = new CloudinaryHttpUploader(new HttpClient(handler), Configuration("Cloudinary:UploadBaseUrl", "https://cloud.example", "Cloudinary:CloudName", "demo", "Cloudinary:ApiKey", "key_123", "Cloudinary:ApiSecret", "secret_456"), new FixedTimeProvider(1_700_000_000));

        var reference = await uploader.UploadAsync("image-bytes");

        Assert.Equal("https://res.cloudinary.com/demo/image/upload/a.png", reference);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/v1_1/demo/auto/upload", handler.PathAndQuery);
        Assert.StartsWith("multipart/form-data", handler.ContentType, StringComparison.Ordinal);
        Assert.Contains("name=api_key", handler.Body, StringComparison.Ordinal);
        Assert.Contains("key_123", handler.Body, StringComparison.Ordinal);
        Assert.Contains("name=timestamp", handler.Body, StringComparison.Ordinal);
        Assert.Contains("1700000000", handler.Body, StringComparison.Ordinal);
        Assert.Contains("name=signature", handler.Body, StringComparison.Ordinal);
        Assert.Contains(Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes("timestamp=1700000000secret_456"))).ToLowerInvariant(), handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Configured_cloudinary_adapter_returns_no_reference_for_provider_failure()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadGateway, "{}");
        var uploader = new CloudinaryHttpUploader(new HttpClient(handler), Configuration("Cloudinary:UploadBaseUrl", "https://cloud.example", "Cloudinary:CloudName", "demo", "Cloudinary:ApiKey", "key_123", "Cloudinary:ApiSecret", "secret_456"), new FixedTimeProvider(1_700_000_000));

        Assert.Null(await uploader.UploadAsync("image-bytes"));
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("/v1_1/demo/auto/upload", handler.PathAndQuery);
    }
}
