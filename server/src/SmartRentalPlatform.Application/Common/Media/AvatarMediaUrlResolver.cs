using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Application.Common.Media;

public static class AvatarMediaUrlResolver
{
    public static async Task<string?> ResolveAsync(
        IAppDbContext dbContext,
        string? avatarUrl,
        Guid? avatarMediaAssetId,
        CancellationToken cancellationToken = default)
    {
        if (avatarMediaAssetId.HasValue)
        {
            var mediaAssetExists = await dbContext.MediaAssets
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == avatarMediaAssetId.Value &&
                         x.Scope == MediaScope.Avatar &&
                         x.Visibility == MediaVisibility.Public &&
                         x.Status != MediaStatus.PendingUpload &&
                         x.Status != MediaStatus.Deleted,
                    cancellationToken);

            if (mediaAssetExists)
            {
                return PublicMediaPathBuilder.Build(avatarMediaAssetId.Value);
            }
        }

        return IsExternalUrl(avatarUrl) ? avatarUrl!.Trim() : null;
    }

    private static bool IsExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsedUri) &&
               (parsedUri.Scheme == Uri.UriSchemeHttp || parsedUri.Scheme == Uri.UriSchemeHttps);
    }
}
