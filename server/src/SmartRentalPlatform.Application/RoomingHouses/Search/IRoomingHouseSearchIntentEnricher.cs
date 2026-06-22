namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public interface IRoomingHouseSearchIntentEnricher
{
    Task EnrichAsync(
        RoomingHouseSearchIntentContext context,
        CancellationToken cancellationToken = default);
}
