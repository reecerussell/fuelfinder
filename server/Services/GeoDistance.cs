namespace FuelFinder.Api.Services;

public static class GeoDistance
{
    private const double EarthRadiusKm = 6371;

    public static double HaversineKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var deltaLatitude = ToRadians(latitude2 - latitude1);
        var deltaLongitude = ToRadians(longitude2 - longitude1);

        var a = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
                + Math.Cos(ToRadians(latitude1)) * Math.Cos(ToRadians(latitude2)) * Math.Pow(Math.Sin(deltaLongitude / 2), 2);

        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
