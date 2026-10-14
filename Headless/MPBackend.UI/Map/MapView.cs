namespace MPBackend.UI.Map;

/// <summary>A tile to draw: screen position and size (unrotated map frame) plus its data URI when loaded.</summary>
public sealed record TileRect(string Key, double X, double Y, double Size, string? DataUri);

/// <summary>
/// Geometry of one rendered map frame: the viewport in pixels, the centre, the range (top-to-bottom distance,
/// spec mfd.navMap.rangeIndicator) and the resulting integer tile zoom + fractional scale.
/// </summary>
public sealed class MapView
{
    public double Width { get; private set; } = 800;
    public double Height { get; private set; } = 600;
    public double CentreLat { get; private set; }
    public double CentreLng { get; private set; }
    public double RangeMetres { get; private set; } = 500;
    public int Zoom { get; private set; } = 15;
    /// <summary>Screen pixels per world pixel at <see cref="Zoom"/> (0.7 .. 1.4).</summary>
    public double Scale { get; private set; } = 1;
    public double PixelsPerMetre { get; private set; }

    private double centreWorldX, centreWorldY;

    public void Set(double width, double height, double lat, double lng, double range, int minZoom, int maxZoom)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        CentreLat = lat;
        CentreLng = lng;
        RangeMetres = range;
        PixelsPerMetre = Height / range;
        var mpp0 = WebMercator.MetresPerPixelZ0(lat);
        var exact = Math.Log2(PixelsPerMetre * mpp0); // zoom at which one world px = one screen px
        Zoom = Math.Clamp((int)Math.Round(exact), minZoom, maxZoom);
        Scale = PixelsPerMetre * mpp0 / Math.Pow(2, Zoom);
        (centreWorldX, centreWorldY) = WebMercator.ToWorld(lat, lng, Zoom);
    }

    /// <summary>Screen position (unrotated map frame, origin top-left of the viewport) of a location.</summary>
    public (double X, double Y) Project(double lat, double lng)
    {
        var (wx, wy) = WebMercator.ToWorld(lat, lng, Zoom);
        return (Width / 2 + (wx - centreWorldX) * Scale, Height / 2 + (wy - centreWorldY) * Scale);
    }

    public (double Lat, double Lng) Unproject(double x, double y) =>
        WebMercator.FromWorld(centreWorldX + (x - Width / 2) / Scale, centreWorldY + (y - Height / 2) / Scale, Zoom);

    /// <summary>Tiles covering the viewport, including the margin needed when the map is rotated.</summary>
    public IEnumerable<TileRect> Tiles(string provider, TileCache cache)
    {
        var half = Math.Sqrt(Width * Width + Height * Height) / 2 / Scale; // world px half-diagonal
        var n = (long)Math.Pow(2, Zoom);
        var size = WebMercator.TileSize * Scale;
        var x0 = (long)Math.Floor((centreWorldX - half) / WebMercator.TileSize);
        var x1 = (long)Math.Floor((centreWorldX + half) / WebMercator.TileSize);
        var y0 = Math.Max(0, (long)Math.Floor((centreWorldY - half) / WebMercator.TileSize));
        var y1 = Math.Min(n - 1, (long)Math.Floor((centreWorldY + half) / WebMercator.TileSize));
        for (var ty = y0; ty <= y1; ty++)
        for (var tx = x0; tx <= x1; tx++)
        {
            var wrapped = ((tx % n) + n) % n;
            var sx = Width / 2 + (tx * WebMercator.TileSize - centreWorldX) * Scale;
            var sy = Height / 2 + (ty * WebMercator.TileSize - centreWorldY) * Scale;
            yield return new TileRect($"{Zoom}/{wrapped}/{ty}", sx, sy, size, cache.Get(provider, Zoom, wrapped, ty));
        }
    }
}
