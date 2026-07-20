using NetTopologySuite.Geometries;

namespace Solace.TileRenderer.Vector;

// https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles/blob/develop/src/NetTopologySuite.IO.VectorTiles.Mapbox/TileGeometryTransform.cs

internal readonly struct TileGeometryTransform
{
    private readonly Tile _tile;
    private readonly uint _extent;
    private readonly long _top;
    private readonly long _left;

    /// <summary>
    /// Initializes this transformation utility
    /// </summary>
    /// <param name="tile">The tile's bounds</param>
    /// <param name="extent">The tile's extent in pixel. Tiles are always square.</param>
    public TileGeometryTransform(Tile tile, uint extent)
    {
        _tile = tile;
        _extent = extent;

        // Precalculate the resolution of the tile for the specified zoom level.
        ZoomResolution = WebMercatorHandler.Resolution(tile.Zoom, (int)extent);

        var meters = WebMercatorHandler.LatLonToMeters(_tile.Top, _tile.Left);
        (_left, _top) = WebMercatorHandler.FromMetersToPixels(meters, ZoomResolution);
    }

    /// <summary>
    /// The zoom level pixel resolution based on the extent.
    /// </summary>
    public double ZoomResolution { get; }

    /// <summary>
    /// Transforms the coordinate at <paramref name="index"/> of <paramref name="sequence"/> to the tile coordinate system.
    /// The return value is the position relative to the local point at (<paramref name="currentX"/>, <paramref name="currentY"/>).
    /// </summary>
    /// <param name="sequence">The input sequence</param>
    /// <param name="index">The index of the coordinate to transform</param>
    /// <param name="currentX">The current horizontal component of the cursor location. This value is updated.</param>
    /// <param name="currentY">The current vertical component of the cursor location. This value is updated.</param>
    /// <returns>The position relative to the local point at (<paramref name="currentX"/>, <paramref name="currentY"/>).</returns>
    public (int x, int y) Transform(CoordinateSequence sequence, int index, ref int currentX, ref int currentY)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        if (sequence.Count is 0)
        {
            throw new ArgumentException($"{nameof(sequence)} is empty.", nameof(sequence));
        }

        var lon = sequence.GetOrdinate(index, Ordinate.X);
        var lat = sequence.GetOrdinate(index, Ordinate.Y);

        var meters = WebMercatorHandler.LatLonToMeters(lat, lon);
        var pixels = WebMercatorHandler.FromMetersToPixels(meters, ZoomResolution);

        var localX = (int)(pixels.x - _left);
        var localY = (int)(_top - pixels.y);
        var dx = localX - currentX;
        var dy = localY - currentY;
        currentX = localX;
        currentY = localY;

        return (dx, dy);
    }

    /// <summary>
    /// Transforms the point in the local tile pixel coordinates into WGS84 coordinates.
    /// The return value is longitude and latitude of the tile pixel point (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    /// <param name="x">The horizontal component of the point in the tile coordinate system</param>
    /// <param name="y">The vertical component of the point in the tile coordinate system</param>
    /// <returns>WGS84 coordinates of the point in tile "pixel" coordinates (<paramref name="x"/>, <paramref name="y"/>).</returns>
    public (double longitude, double latitude) TransformInverse(int x, int y)
    {
        var globalX = _left + x;
        var globalY = _top - y;

        var meters = WebMercatorHandler.FromPixelsToMeters((globalX, globalY), ZoomResolution);
        var coordinates = WebMercatorHandler.MetersToLatLon(meters);
        return coordinates;
    }

    /// <summary>
    /// Check if the point with tile coordinates (<paramref name="x"/>, <paramref name="y"/> lies inside tile extent
    /// </summary>
    /// <param name="x">Horizontal component of the point in the tile coordinate system</param>
    /// <param name="y">Vertical component of the point in the tile coordinate system</param>
    /// <returns>true if point lies inside tile extent</returns>
    public bool IsPointInExtent(int x, int y)
        => x >= 0 && y >= 0 && x < _extent && y < _extent;

    /// <summary>
    /// Checks to see if a geometries envelope is greater than 1 square pixel in size for a specified zoom level.
    /// </summary>
    /// <param name="polygon">Polygon to test.</param>
    /// <returns>true if the <paramref name="polygon"/> is greater than 1 pixel in the tile pixel coordinates</returns>
    public bool IsGreaterThanOnePixelOfTile(Geometry geometry)
    {
        if (geometry.IsEmpty)
        {
            return false;
        }

        var env = geometry.EnvelopeInternal;
        (double x1, double y1) = WebMercatorHandler.FromMetersToPixels(WebMercatorHandler.LatLonToMeters(env.MinY, env.MinX), ZoomResolution);
        (double x2, double y2) = WebMercatorHandler.FromMetersToPixels(WebMercatorHandler.LatLonToMeters(env.MaxY, env.MaxX), ZoomResolution);

        var dx = Math.Abs(x2 - x1);
        var dy = Math.Abs(y2 - y1);

        // Both must be greater than 0, and at least one of them needs to be larger than 1. 
        return dx > 0 && dy > 0 && (dx > 1 || dy > 1);
    }

    public (long x, long y) ExtentInPixel(Envelope env)
    {
        (var minX, var minY) = WebMercatorHandler.FromMetersToPixels(WebMercatorHandler.LatLonToMeters(env.MinY, env.MinX), ZoomResolution);
        (var maxX, var maxY) = WebMercatorHandler.FromMetersToPixels(WebMercatorHandler.LatLonToMeters(env.MaxY, env.MaxX), ZoomResolution);

        return (maxX - minX, maxY - minY);
    }
}