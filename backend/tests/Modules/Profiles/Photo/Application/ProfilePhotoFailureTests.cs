using Microsoft.EntityFrameworkCore;

namespace IoBuild.Modules.Tests.Profiles.Management.Api;

public sealed partial class ProfileAccessTests
{
    [Fact]
    [Trait("Flow", "PROFILES.MANAGE")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public async Task PHOTO_failed_upload_aborts_without_touching_the_stored_photo()
    {
        await using var db = CreateDb();
        db.Profiles.Add(new IoBuild.Api.Profiles.Domain.Model.Aggregates.Profile { UserId = 29, Name = "Q", Username = "q29", PhotoReference = "ref-keep", PhotoUrl = "https://cloud.test/keep" });
        await db.SaveChangesAsync();

        var workflow = new IoBuild.Api.Profiles.Application.Internal.CommandServices.ProfilePhotoWorkflow(db, new BrokenUploader());
        Assert.False(await workflow.ReplaceAsync(29, "ref-keep", "new-bytes"));

        var untouched = await db.Profiles.SingleAsync(p => p.UserId == 29);
        Assert.Equal("ref-keep", untouched.PhotoReference);
        Assert.Equal("https://cloud.test/keep", untouched.PhotoUrl);
    }
}
