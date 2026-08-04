namespace Solace.TileRenderer.Vector;

// https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles/blob/develop/src/NetTopologySuite.IO.VectorTiles/Tiles/Tile.cs

public sealed class Tile
{
    private readonly ulong _id;

    /// <summary>
    /// Creates a new tile from a given id.
    /// </summary>
    /// <param name="id"></param>
    public Tile(ulong id)
    {
        _id = id;

        var (x, y, zoom) = CalculateTile(id);
        X = x;
        Y = y;
        Zoom = zoom;
        CalculateBounds();
    }

    /// <summary>
    /// Creates a new tile.
    /// </summary>
    public Tile(int x, int y, int zoom)
    {
        X = x;
        Y = y;
        Zoom = zoom;

        _id = CalculateTileId(zoom, x, y);
        CalculateBounds();
    }

    private void CalculateBounds()
    {
        var bbox = GetBoundingBox(X, Y, Zoom);

        Left = bbox.Left;
        Bottom = bbox.Bottom;
        Right = bbox.Right;
        Top = bbox.Top;

        CenterLat = (double)((Top + Bottom) / 2.0);
        CenterLon = (double)((Left + Right) / 2.0);
    }

    /// <summary>
    /// Get the bounding box for a xyz tile.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="zoom"></param>
    internal static BoundingBox GetBoundingBox(int x, int y, int zoom)
    {
        var zoomPow = (double)(1 << zoom); // double.Pow(2.0, this.Zoom)
        var n = double.Pi - ((2.0 * double.Pi * y) / zoomPow);
        var left = (x / zoomPow * 360.0) - 180.0;
        var top = 180.0 / double.Pi * double.Atan(double.Sinh(n));

        n = double.Pi - ((2.0 * double.Pi * (y + 1)) / zoomPow);
        var right = ((x + 1) / zoomPow * 360.0) - 180.0;
        var bottom = 180.0 / double.Pi * double.Atan(double.Sinh(n));
        return new BoundingBox(left, bottom, right, top);
    }

    /// <summary>
    /// The X position of the tile.
    /// </summary>
    public int X { get; private set; }

    /// <summary>
    /// The Y position of the tile.
    /// </summary>
    public int Y { get; private set; }

    /// <summary>
    /// The zoom level for this tile.
    /// </summary>
    public int Zoom { get; private set; }

    /// <summary>
    /// Gets the top.
    /// </summary>
    public double Top { get; private set; }

    /// <summary>
    /// Get the bottom.
    /// </summary>
    public double Bottom { get; private set; }

    /// <summary>
    /// Get the left.
    /// </summary>
    public double Left { get; private set; }

    /// <summary>
    /// Gets the right.
    /// </summary>
    public double Right { get; private set; }

    /// <summary>
    /// Gets the center lat.
    /// </summary>
    public double CenterLat { get; private set; }

    /// <summary>
    /// Gets the center lon.
    /// </summary>
    public double CenterLon { get; private set; }

    /// <summary>
    /// Gets the parent tile.
    /// </summary>
    public Tile Parent => new(X / 2, Y / 2, Zoom - 1);

    /// <summary>
    /// Returns a hashcode for this tile position.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
        => HashCode.Combine(X, Y, Zoom);

    /// <summary>
    /// Returns true if the given object represents the same tile.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj)
        => obj is Tile other && X == other.X && Y == other.Y && Zoom == other.Zoom;

    /// <summary>
    /// Returns a description for this tile.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
        => $"{X}x-{Y}y@{Zoom}z";

    /// <summary>
    /// Returns true if the given tiles are direct neighbours.
    /// </summary>
    /// <param name="tileId1">The first tile id.</param>
    /// <param name="tileId2">The second tile id.</param>
    /// <returns></returns>
    public static bool IsDirectNeighbour(ulong tileId1, ulong tileId2)
    {
        if (tileId1 == tileId2)
        {
            return false;
        }

        (var x1, var y1, var zoom1) = CalculateTile(tileId1);
        (var x2, var y2, var zoom2) = CalculateTile(tileId2);

        if (zoom1 != zoom2)
        {
            return false;
        }

        if (x1 == x2)
        {
            return (y1 == y2 + 1) || (y1 == y2 - 1);
        }
        else if (y1 == y2)
        {
            return (x1 == x2 + 1) || (x1 == x2 - 1);
        }

        return false;
    }

    /// <summary>
    /// Calculates the tile id of the tile at position (0, 0) for the given zoom.
    /// </summary>
    /// <param name="zoom"></param>
    /// <returns></returns>
    public static ulong CalculateTileId(int zoom)
        => zoom switch
        {
            0 => 0,
            1 => 1,
            2 => 5,
            3 => 21,
            4 => 85,
            5 => 341,
            6 => 1365,
            7 => 5461,
            8 => 21845,
            9 => 87381,
            10 => 349525,
            11 => 1398101,
            12 => 5592405,
            13 => 22369621,
            14 => 89478485,
            15 => 357913941,
            16 => 1431655765,
            17 => 5726623061,
            18 => 22906492245,
            19 => 91625968981,
            20 => 366503875925,
            21 => 1466015503701,
            22 => 5864062014805,
            23 => 23456248059221,
            24 => 93824992236885,
            //Calculate the tileId if zoom level doesn't match one of the above precalculated values.
            _ => (ulong)(double.Pow(4, zoom) - 1) / 3,
        };

    /// <summary>
    /// Calculates the tile id of the tile at position (x, y) for the given zoom.
    /// </summary>
    /// <param name="zoom"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static ulong CalculateTileId(int zoom, int x, int y)
    {
        var id = CalculateTileId(zoom);
        var width = (long)(1 << zoom);// double.Pow(2, zoom);
        return id + (ulong)x + (ulong)(y * width);
    }

    /// <summary>
    /// Calculate the tile given the id.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static (int x, int y, int zoom) CalculateTile(ulong id)
    {
        // find out the zoom level first.
        var zoom = 0;
        if (id > 0)
        {
            // only if the id is at least at zoom level 1.
            while (id >= CalculateTileId(zoom))
            {
                // move to the next zoom level and keep searching.
                zoom++;
            }

            zoom--;
        }

        // calculate the x-y.
        var local = id - Tile.CalculateTileId(zoom);
        var width = (ulong)(1 << zoom);// double.Pow(2, zoom);
        var x = (int)(local % width);
        var y = (int)(local / width);

        return (x, y, zoom);
    }

    /// <summary>
    /// Returns the id of this tile.
    /// </summary>
    public ulong Id => _id;

    /// <summary>
    /// Returns true if this tile is valid.
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (X < 0 || Y < 0 || Zoom < 0)
            {
                return false; // some are negative.
            }

            var size = (double)(1 << Zoom); // double.Pow(2, this.Zoom);
            return X < size && Y < size;
        }
    }

    /// <summary>
    /// Returns the tile at the given location at the given zoom.
    /// </summary>
    public static Tile? CreateAroundLocation(double lat, double lon, int zoom)
    {
        if (!CreateAroundLocation(lat, lon, zoom, out var x, out var y))
        {
            return null;
        }

        return new Tile(x, y, zoom);
    }

    /// <summary>
    /// Returns the tile at the given location at the given zoom.
    /// </summary>
    public static ulong CreateAroundLocationId(double lat, double lon, int zoom)
    {
        if (!CreateAroundLocation(lat, lon, zoom, out var x, out var y))
        {
            return ulong.MaxValue;
        }

        return Tile.CalculateTileId(zoom, x, y);
    }

    /// <summary>
    /// A fast method of calculating x-y without creating a tile object.
    /// </summary>
    public static bool CreateAroundLocation(double lat, double lon, int zoom, out int x, out int y)
    {
        if (lon == 180)
        {
            lon -= 0.000001;
        }

        if (lat > 85.0511 || lat < -85.0511)
        {
            x = 0;
            y = 0;
            return false;
        }

        var scale = (double)(1 << zoom);

        x = (int)((lon + 180.0) / 360.0 * scale);
        var latRad = lat * double.Pi / 180.0;
        y = (int)((1.0 - double.Log(double.Tan(latRad) + 1.0 / double.Cos(latRad)) / double.Pi) / 2.0 * scale);
        return true;
    }

    /// <summary>
    /// Gets the tile id the given lat/lon belongs to one zoom level lower.
    /// </summary>
    public ulong GetSubTileIdFor(double lat, double lon)
    {
        const int factor = 2;
        var zoom = Zoom + 1;
        int x = 0, y = 0;
        if (lat >= CenterLat && lon < CenterLon)
        {
            x = X * factor;
            y = Y * factor;
        }
        else if (lat >= CenterLat && lon >= CenterLon)
        {
            x = X * factor + factor - 1;
            y = Y * factor;
        }
        else if (lat < CenterLat && lon < CenterLon)
        {
            x = X * factor;
            y = Y * factor + factor - 1;
        }
        else if (lat < CenterLat && lon >= CenterLon)
        {
            x = X * factor + factor - 1;
            y = Y * factor + factor - 1;
        }

        return CalculateTileId(zoom, x, y);
    }

    /// <summary>
    /// Returns the subtiles of this tile at the given zoom.
    /// </summary>
    public TileRange GetSubTiles(int zoom)
    {
        if (Zoom > zoom)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom),
                "Subtiles can only be calculated for higher zooms.");
        }

        if (Zoom == zoom)
        {
            // just return a range of one tile.
            return new TileRange(X, Y, X, Y, Zoom);
        }

        var factor = 1 << (zoom - Zoom);

        return new TileRange(
            X * factor,
            Y * factor,
            X * factor + factor - 1,
            Y * factor + factor - 1,
            zoom);
    }

    /// <summary>
    /// Inverts the X-coordinate.
    /// </summary>
    /// <returns></returns>
    public Tile InvertX()
    {
        var n = 1 << Zoom;// double.Floor(double.Pow(2, this.Zoom));

        return new Tile(n - X - 1, Y, Zoom);
    }

    /// <summary>
    /// Inverts the Y-coordinate.
    /// </summary>
    /// <returns></returns>
    public Tile InvertY()
    {
        var n = 1 << Zoom; // double.Floor(double.Pow(2, this.Zoom));

        return new Tile(X, n - Y - 1, Zoom);
    }

    internal (double x, double y) SubCoordinates(double lat, double lon)
    {
        var leftOffset = lon - Left;
        var bottomOffset = lat - Bottom;

        return (X + (leftOffset / (Right - Left)),
            Y + (bottomOffset / (Top - Bottom)));
    }
}