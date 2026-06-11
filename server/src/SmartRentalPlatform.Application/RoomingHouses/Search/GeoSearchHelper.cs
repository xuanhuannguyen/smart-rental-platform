using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public static class GeoSearchHelper
{
    public const decimal DefaultRadiusKm = 3m;
    public const decimal MinRadiusKm = 0.5m;
    public const decimal MaxRadiusKm = 30m;

    private const double EarthRadiusKm = 6371.0088;

    public static decimal CalculateDistanceKm(decimal fromLat, decimal fromLng, decimal toLat, decimal toLng)
    {
        var lat1 = ToRadians((double)fromLat);
        var lat2 = ToRadians((double)toLat);
        var deltaLat = ToRadians((double)(toLat - fromLat));
        var deltaLng = ToRadians((double)(toLng - fromLng));

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
            + Math.Cos(lat1) * Math.Cos(lat2)
            * Math.Sin(deltaLng / 2) * Math.Sin(deltaLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round((decimal)(EarthRadiusKm * c), 3);
    }

    public static GeoBoundingBox BuildBoundingBox(decimal centerLat, decimal centerLng, decimal radiusKm)
    {
        var latDelta = radiusKm / 111m;
        var centerLatRadians = ToRadians((double)centerLat);
        var lngDivisor = 111m * Math.Max(0.1m, (decimal)Math.Cos(centerLatRadians));
        var lngDelta = radiusKm / lngDivisor;

        return new GeoBoundingBox(
            centerLat - latDelta,
            centerLat + latDelta,
            centerLng - lngDelta,
            centerLng + lngDelta);
    }

    public static void ValidateCoordinates(decimal? centerLat, decimal? centerLng)
    {
        if (centerLat is < -90m or > 90m)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Vĩ độ tâm tìm kiếm phải nằm trong khoảng từ -90 đến 90.",
                new { field = nameof(centerLat) });
        }

        if (centerLng is < -180m or > 180m)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Kinh độ tâm tìm kiếm phải nằm trong khoảng từ -180 đến 180.",
                new { field = nameof(centerLng) });
        }

        if ((centerLat is null) != (centerLng is null))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Cần truyền cả vĩ độ và kinh độ tâm tìm kiếm.",
                new { fields = new[] { nameof(centerLat), nameof(centerLng) } });
        }
    }

    public static decimal? NormalizeRadius(decimal? radiusKm, bool hasPlaceText)
    {
        if (radiusKm is null)
        {
            return hasPlaceText ? DefaultRadiusKm : null;
        }

        if (radiusKm < MinRadiusKm || radiusKm > MaxRadiusKm)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                $"Bán kính tìm kiếm phải nằm trong khoảng từ {MinRadiusKm} đến {MaxRadiusKm} km.",
                new { field = nameof(radiusKm) });
        }

        return radiusKm;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }
}

public sealed record GeoBoundingBox(
    decimal MinLat,
    decimal MaxLat,
    decimal MinLng,
    decimal MaxLng);
