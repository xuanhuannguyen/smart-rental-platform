using Microsoft.Extensions.Caching.Memory;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IPublicRoomingHouseCacheInvalidator
{
    long CurrentVersion { get; }

    void Invalidate();
}

public sealed class PublicRoomingHouseCacheInvalidator : IPublicRoomingHouseCacheInvalidator
{
    private const string VersionKey = "public-rooming-house-cache-version";
    private readonly IMemoryCache memoryCache;

    public PublicRoomingHouseCacheInvalidator(IMemoryCache memoryCache)
    {
        this.memoryCache = memoryCache;
    }

    public long CurrentVersion => memoryCache.GetOrCreate(VersionKey, entry =>
    {
        entry.Priority = CacheItemPriority.NeverRemove;
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    });

    public void Invalidate()
    {
        memoryCache.Set(
            VersionKey,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
    }
}
