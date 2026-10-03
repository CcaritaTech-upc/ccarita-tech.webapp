using IoBuild.Api.Profiles.Application.Internal.CommandServices;
using IoBuild.Api.Profiles.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Profiles.Photo.Application;

[Trait("Context", "Profiles")]
[Trait("Capability", "Photo")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class ProfilePhotoWorkflowTests
{
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Failed_cloudinary_upload_keeps_the_existing_content_addressed_reference()
    {
        await using var db = CreateDb();
        db.Profiles.Add(new Profile { UserId = 7, Name = "Ada", Username = "ada", PhotoReference = "sha256:existing" });
        await db.SaveChangesAsync();
        var workflow = new ProfilePhotoWorkflow(db, new FailingCloudinaryUploader());
        var updated = await workflow.ReplaceAsync(7, "sha256:existing", "image-bytes");
        Assert.False(updated);
        Assert.Equal("sha256:existing", (await db.Profiles.SingleAsync()).PhotoReference);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Successful_cloudinary_upload_persists_a_content_addressed_reference()
    {
        await using var db = CreateDb();
        db.Profiles.Add(new Profile { UserId = 8, Name = "Grace", Username = "grace", PhotoReference = "sha256:existing" });
        await db.SaveChangesAsync();
        var workflow = new ProfilePhotoWorkflow(db, new SuccessfulCloudinaryUploader());
        Assert.True(await workflow.ReplaceAsync(8, "sha256:existing", "image-bytes"));

        var profile = await db.Profiles.SingleAsync();
        Assert.Equal("sha256:2c8648d103e3dd7ad87660da0f126a1443b6d21ac1bd3ec000c5e24e2373a90c", profile.PhotoReference);
        Assert.Equal("cloudinary://asset", profile.CloudinaryReference);
        Assert.Equal("cloudinary://asset", profile.PhotoUrl);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Cloudinary_cas_mismatch_does_not_overwrite_the_profile()
    {
        await using var db = CreateDb();
        db.Profiles.Add(new Profile { UserId = 22, Name = "Ada", Username = "ada", PhotoReference = "sha256:expected" });
        await db.SaveChangesAsync();

        var result = await new ProfilePhotoWorkflow(db, new SuccessfulCloudinaryUploader()).ReplaceAsync(22, "sha256:stale", "image-bytes");

        Assert.False(result);
        Assert.Equal("sha256:expected", (await db.Profiles.SingleAsync()).PhotoReference);
    }
}
