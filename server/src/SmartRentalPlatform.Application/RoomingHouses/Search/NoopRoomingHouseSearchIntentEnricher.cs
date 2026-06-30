namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class NoopRoomingHouseSearchIntentEnricher : IRoomingHouseSearchIntentEnricher
{
    public Task EnrichAsync(
        RoomingHouseSearchIntentContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
