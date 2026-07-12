namespace Solace.TileRenderer.Vector;

// https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles/blob/develop/src/NetTopologySuite.IO.VectorTiles/Tiles/WebMercator/WebMercatorHandler.cs

public static class WebMercatorHandler
{
    // https://gist.github.com/nagasudhirpulla/9b5a192ccaca3c5992e5d4af0d1e6dc4

    private const int EarthRadius = 6378137;
    private const double OriginShift = 2 * double.Pi * EarthRadius / 2;

    /// <summary>
    /// Converts given lat/lon in WGS84 Datum to XY in Spherical Mercator EPSG:900913
    /// </summary>
    /// <param name="lat">Latitude of the point</param>
    /// <param name="lon">Longitude of the point</param>
    /// <returns>Point (x, y) in the Web Mercator coordinate system</returns>
    public static (double x, double y) LatLonToMeters(double lat, double lon)
    {
        double x = lon * OriginShift / 180;
        double y = double.Log(double.Tan((90 + lat) * double.Pi / 360)) / (double.Pi / 180);
        y = y * OriginShift / 180;
        return (x, y);
    }

    /// <summary>
    /// Converts XY point from (Spherical) Web Mercator EPSG:3857 (previously EPSG:3785, unofficially EPSG:900913) to lat/lon in WGS84 Datum
    /// </summary>
    /// <param name="m">Point (x, y) in the Web Mercator coordinate system</param>
    /// <returns>Point (lat, lon) in the WGS84 coordinate system</returns>
    public static (double x, double y) MetersToLatLon((double x, double y) m)
    {
        double x = m.x / OriginShift * 180;
        double y = m.y / OriginShift * 180;
        y = 180 / double.Pi * (2 * double.Atan(double.Exp(y * double.Pi / 180)) - double.Pi / 2);

        //Clamp latitude value to acceptable range.
        y = double.Min(double.Max(y, -85.0511), 85.0511);

        return (x, y);
    }

    /// <summary>
    /// Converts EPSG:3857 to pyramid pixel coordinates in given zoom level
    /// </summary>
    /// <param name="m">Point (x, y) in the Web Mercator coordinate system</param>
    /// <param name="zoom">Scale level at which the conversion to pixel coordinates is performed</param>
    /// <param name="tileSize">Tile size. Used to determine the resolution</param>
    /// <returns>Point (x, y) on the virtual raster of a specified resolution covering the entire world</returns>
    [Obsolete("MetersToPixels is deprecated, please use FromMetersToPixels instead.")]
    public static (double x, double y) MetersToPixels((double x, double y) m, int zoom, int tileSize = 512)
    {
        double res = Resolution(zoom, tileSize);
        return MetersToPixels(m, res);
    }

    /// <summary>
    /// Converts EPSG:3857 to pyramid pixel coordinates in given zoom level
    /// </summary>
    /// <param name="m">Point (x, y) in the Web Mercator coordinate system</param>
    /// <param name="zoom">Scale level at which the conversion to pixel coordinates is performed</param>
    /// <param name="tileSize">Tile size. Used to determine the resolution</param>
    /// <returns>Point (x, y) on the virtual raster of a specified resolution covering the entire world</returns>
    /// <remarks>Pixel coordinates are truncated to integer values</remarks>
    public static (long x, long y) FromMetersToPixels((double x, double y) m, int zoom, int tileSize = 512)
    {
        double res = Resolution(zoom, tileSize);
        return FromMetersToPixels(m, res);
    }

    /// <summary>
    /// Converts EPSG:3857 to pyramid pixel coordinates for given zoom level resolution. In this case zoomlevel resolution is precalculated for performance.
    /// </summary>
    /// <param name="m">Point (x, y) in the Web Mercator coordinate system</param>
    /// <param name="res">Resolution</param>
    /// <returns>Point (x, y) on the virtual raster of a specified resolution covering the entire world</returns>
    [Obsolete("MetersToPixels is deprecated, please use FromMetersToPixels instead.")]
    public static (double x, double y) MetersToPixels((double x, double y) m, double res)
    {
        double x = (m.x + OriginShift) / res;
        double y = (m.y + OriginShift) / res;
        return (x, y);
    }

    /// <summary>
    /// Converts EPSG:3857 to pyramid pixel coordinates for given zoom level resolution. In this case zoomlevel resolution is precalculated for performance.
    /// </summary>
    /// <param name="m">Point (x, y) in the Web Mercator coordinate system</param>
    /// <param name="res">Resolution</param>
    /// <returns>Point (x, y) on the virtual raster of a specified resolution covering the entire world</returns>
    /// <remarks>Pixel coordinates are truncated to integer values</remarks>
    public static (long x, long y) FromMetersToPixels((double x, double y) m, double res)
    {
        long x = (long)((m.x + OriginShift) / res);
        long y = (long)((m.y + OriginShift) / res);
        return (x, y);
    }

    /// <summary>
    /// Converts pixel coordinates in given zoom level of pyramid to EPSG:3857
    /// </summary>
    /// <param name="p">Point (x, y) on the virtual raster of a specified resolution covering the entire world</param>
    /// <param name="zoom">Scale level at which the conversion to pixel coordinates is performed</param>
    /// <param name="tileSize">Tile size. Used to determine the resolution</param>
    /// <returns>Point (x, y) in the Web Mercator coordinate system</returns>
    [Obsolete("PixelsToMeters is deprecated, please use FromPixelsToMeters instead.")]
    public static (double x, double y) PixelsToMeters((double x, double y) p, int zoom, int tileSize = 512)
    {
        double res = Resolution(zoom, tileSize);
        return PixelsToMeters(p, res);
    }

    /// <summary>
    /// Converts pixel coordinates in given zoom level of pyramid to EPSG:3857
    /// </summary>
    /// <param name="p">Point (x, y) on the virtual raster of a specified resolution covering the entire world</param>
    /// <param name="zoom">Scale level at which the conversion to pixel coordinates is performed</param>
    /// <param name="tileSize">Tile size. Used to determine the resolution</param>
    /// <returns>Point (x, y) in the Web Mercator coordinate system</returns>
    public static (double x, double y) FromPixelsToMeters((long x, long y) p, int zoom, int tileSize = 512)
    {
        double res = Resolution(zoom, tileSize);
        return FromPixelsToMeters(p, res);
    }

    /// <summary>
    /// Converts pixel coordinates in given zoom level resolution of pyramid to EPSG:3857. In this case zoomlevel resolution is precalculated for performance.
    /// </summary>
    /// <param name="p">Point (x, y) on the virtual raster of a specified resolution covering the entire world</param>
    /// <param name="res">Resolution</param>
    /// <returns>Point (x, y) in the Web Mercator coordinate system</returns>
    [Obsolete("PixelsToMeters is deprecated, please use FromPixelsToMeters instead.")]
    public static (double x, double y) PixelsToMeters((double x, double y) p, double res)
    {
        double x = p.x * res - OriginShift;
        double y = p.y * res - OriginShift;
        return (x, y);
    }

    /// <summary>
    /// Converts pixel coordinates in given zoom level resolution of pyramid to EPSG:3857. In this case zoomlevel resolution is precalculated for performance.
    /// </summary>
    /// <param name="p">Point (x, y) on the virtual raster of a specified resolution covering the entire world</param>
    /// <param name="res">Resolution</param>
    /// <returns>Point (x, y) in the Web Mercator coordinate system</returns>
    public static (double x, double y) FromPixelsToMeters((long x, long y) p, double res)
    {
        double x = p.x * res - OriginShift;
        double y = p.y * res - OriginShift;
        return (x, y);
    }

    /// <summary>
    /// Resolution (meters/pixel) for given zoom level (measured at Equator)
    /// </summary>
    /// <param name="zoom">Zoom level</param>
    /// <param name="tileSize">Tile size. Used to determine the resolution</param>
    /// <returns>Resolution of the virtual raster</returns>
    public static double Resolution(int zoom, int tileSize = 512)
        => InitialResolution(tileSize) / (1 << zoom);// (double.Pow(2, zoom));

    /// <summary>
    /// Resolution (meters/pixel) at the zero scale level of the pyramid (measured at Equator)
    /// </summary>
    /// <param name="tileSize">Tile size</param>
    /// <returns>Resolution of the virtual raster at the zero scale level of the pyramid</returns>
    public static double InitialResolution(int tileSize = 512)
        => 2 * double.Pi * EarthRadius / tileSize;
}