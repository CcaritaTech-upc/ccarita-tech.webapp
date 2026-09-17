using System.Security.Cryptography;
using System.Text;
using IoBuild.Api.Persistence;
using IoBuild.Api.Profiles.Domain.Model.Aggregates;
using IoBuild.Api.Profiles.Infrastructure.Cloudinary;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Profiles.Application.Internal.CommandServices;

/// <summary>
/// Profiles application workflow (Cloudinary). Moved from CoreBusinessServices.cs
/// to Profiles/Application for DDD clarity, but keeps IoBuild.Api.CoreBusiness
/// namespace for test compatibility.
/// </summary>
public sealed class ProfilePhotoWorkflow(IoBuildDbContext dbContext, ICloudinaryUploader uploader)
{
    public async Task<bool> ReplaceAsync(int userId, string expectedReference, string imageContent, CancellationToken cancellationToken = default)
    {
        var uploadedReference = await uploader.UploadAsync(imageContent, cancellationToken);
        if (string.IsNullOrWhiteSpace(uploadedReference)) return false;

        var profile = await dbContext.Profiles.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        // A fresh profile has no reference yet: null bootstraps as empty so the
        // first replacement is reachable; afterwards the swap is strict.
        if (profile is null || !string.Equals(profile.PhotoReference ?? string.Empty, expectedReference, StringComparison.Ordinal)) return false;
        profile.PhotoReference = $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imageContent))).ToLowerInvariant()}";
        profile.CloudinaryReference = uploadedReference;
        profile.PhotoUrl = uploadedReference;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
