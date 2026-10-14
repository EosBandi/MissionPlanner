namespace MPBackend.UI.Map;

/// <summary>Web Mercator (EPSG:3857) with 256 px tiles, as GMap.NET's XYZ providers use.</summary>
public static class WebMercator
{
    public const int TileSize = 256;
    private const double Deg = Math.PI / 180;
    private const double EarthCircumference = 40075016.686;

    /// <summary>World pixel coordinates at a zoom level.</summary>
    public static (double X, double Y) ToWorld(double lat, double lng, int zoom)
    {
        var n = TileSize * Math.Pow(2, zoom);
        lat = Math.Clamp(lat, -85.05112878, 85.05112878);
        var x = (lng + 180) / 360 * n;
        var sin = Math.Sin(lat * Deg);
        var y = (0.5 - Math.Log((1 + sin) / (1 - sin)) / (4 * Math.PI)) * n;
        return (x, y);
    }

    public static (double Lat, double Lng) FromWorld(double x, double y, int zoom)
    {
        var n = TileSize * Math.Pow(2, zoom);
        var lng = x / n * 360 - 180;
        var lat = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n))) / Deg;
        return (lat, lng);
    }

    /// <summary>Ground metres per world pixel at zoom 0 for this latitude.</summary>
    public static double MetresPerPixelZ0(double lat) => EarthCircumference / TileSize * Math.Cos(lat * Deg);

    /// <summary>Great-circle distance in metres.</summary>
    public static double Distance(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6371008.8;
        var dLat = (lat2 - lat1) * Deg;
        var dLng = (lng2 - lng1) * Deg;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Deg) * Math.Cos(lat2 * Deg) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * r * Math.Asin(Math.Sqrt(a));
    }

    public static double Bearing(double lat1, double lng1, double lat2, double lng2)
    {
        var p1 = lat1 * Deg;
        var p2 = lat2 * Deg;
        var dl = (lng2 - lng1) * Deg;
        var y = Math.Sin(dl) * Math.Cos(p2);
        var x = Math.Cos(p1) * Math.Sin(p2) - Math.Sin(p1) * Math.Cos(p2) * Math.Cos(dl);
        return ((Math.Atan2(y, x) / Deg) % 360 + 360) % 360;
    }
}
