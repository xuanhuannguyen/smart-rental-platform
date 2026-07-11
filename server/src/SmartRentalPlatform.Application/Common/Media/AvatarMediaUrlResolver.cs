using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Application.Common.Media;

public static class AvatarMediaUrlResolver
{
    public static async Task<string?> ResolveAsync(
        IAppDbContext dbContext,
        string? avatarUrl,
        Guid? avatarMediaAssetId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            return avatarUrl.Trim();
        }

        if (!avatarMediaAssetId.HasValue)
        {
            return null;
        }

        var objectKey = await dbContext.MediaAssets
            .AsNoTracking()
            .Where(x => x.Id == avatarMediaAssetId.Value)
            .Select(x => x.ObjectKey)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(objectKey)
            ? null
            : PublicMediaPathBuilder.Build(objectKey);
    }
}
