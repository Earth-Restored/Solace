namespace Solace.TileRenderer.Vector;

// https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles/blob/develop/src/NetTopologySuite.IO.VectorTiles/Tiles/TileRange.cs

/// <summary>
/// Represents a range of tiles.
/// </summary>
public sealed class TileRange : IEnumerable<Tile?>
{
    /// <summary>
    /// Creates a new tile range.
    /// </summary>
    public TileRange(int xMin, int yMin, int xMax, int yMax, int zoom)
    {
        XMin = xMin;
        XMax = xMax;
        YMin = yMin;
        YMax = yMax;

        Zoom = zoom;
    }

    /// <summary>
    /// The minimum X of this range.
    /// </summary>
    public int XMin { get; private set; }

    /// <summary>
    /// The minimum Y of this range.
    /// </summary>
    public int YMin { get; private set; }

    /// <summary>
    /// The maximum X of this range.
    /// </summary>
    public int XMax { get; private set; }

    /// <summary>
    /// The maximum Y of this range.
    /// </summary>
    public int YMax { get; private set; }

    /// <summary>
    /// The zoom of this range.
    /// </summary>
    public int Zoom { get; private set; }

    /// <summary>
    /// Returns the number of tiles in this range.
    /// </summary>
#pragma warning disable IDE0004 // Remove Unnecessary Cast
    public long Count => long.Abs((long)XMax - (long)XMin + 1) * long.Abs((long)YMax - (long)YMin + 1);
#pragma warning restore IDE0004 // Remove Unnecessary Cast

    /// <summary>
    /// Returns true if the given tile exists in this range.
    /// </summary>
    /// <param name="tile"></param>
    /// <returns></returns>
    public bool Contains(Tile tile)
        => XMax >= tile.X && XMin <= tile.X &&
            YMax >= tile.Y && YMin <= tile.Y;

    #region Functions

    /// <summary>
    /// Returns true if the given tile lies at the border of this range.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="zoom"></param>
    /// <returns></returns>
    public bool IsBorderAt(int x, int y, int zoom)
        => ((x == XMin) || (x == XMax)
            || (y == YMin) || (y == YMin)) &&
            Zoom == zoom;

    /// <summary>
    /// Returns true if the given tile lies at the border of this range.
    /// </summary>
    /// <param name="tile"></param>
    /// <returns></returns>
    public bool IsBorderAt(Tile tile)
        => IsBorderAt(tile.X, tile.Y, tile.Zoom);

    #endregion

    /// <summary>
    /// Returns en enumerator of tiles.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<Tile?> GetEnumerator()
        => new TileRangeEnumerator(this);

    /// <summary>
    /// Returns en enumerator of tiles.
    /// </summary>
    /// <returns></returns>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => GetEnumerator();

    /// <summary>
    /// Simple enumerator.
    /// </summary>
    private sealed class TileRangeEnumerator : IEnumerator<Tile?>
    {
        private readonly TileRange _range;

        private Tile? _current;

        public TileRangeEnumerator(TileRange range)
        {
            _range = range;
        }

        public Tile? Current => _current;

        public void Dispose()
        {
        }

        object? System.Collections.IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_current is null)
            {
                _current = new Tile(_range.XMin, _range.YMin, _range.Zoom);
                return true;
            }

            var x = _current.X;
            var y = _current.Y;

            if (x == _range.XMax)
            {
                if (y == _range.YMax)
                {
                    return false;
                }

                y++;
                x = _range.XMin;
            }
            else
            {
                x++;
            }

            _current = new Tile(x, y, _current.Zoom);
            return true;
        }

        public void Reset()
            => _current = null;
    }

    /// <summary>
    /// Defines an enumerator that start at the center of the range and moves out in a spiral.
    /// </summary>
    public sealed class TileRangeCenteredEnumerator : IEnumerator<Tile?>
    {
        private readonly TileRange _range;
        private Tile? _current;
        private readonly HashSet<Tile> _enumeratedTiles = [];

        /// <summary>
        /// Creates the enumerator.
        /// </summary>
        /// <param name="range"></param>
        public TileRangeCenteredEnumerator(TileRange range)
        {
            _range = range;
        }

        /// <summary>
        /// Returns the current tile.
        /// </summary>
        public Tile? Current => _current;

        /// <inhertdoc/>
        public void Dispose()
        {
        }

        /// <summary>
        /// Returns the current tile.
        /// </summary>
        object? System.Collections.IEnumerator.Current => Current;

        /// <summary>
        /// Holds the current desired direction.
        /// </summary>
        private DirectionEnum _direction = DirectionEnum.Up;

        private enum DirectionEnum
        {
            Up = 0,
            Right = 1,
            Down = 2,
            Left = 3,
        }

        /// <summary>
        /// Move to the next tile.
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            if (_current is null)
            { 
                // start with the center tile.
                var centerX = (int)double.Floor((_range.XMax + _range.XMin) / 2.0);
                var centerY = (int)double.Ceiling((_range.YMax + _range.YMin) / 2.0);
                _current = new Tile(centerX, centerY, _range.Zoom);
                _enumeratedTiles.Add(_current);
                return true;
            }

            // check if there are more tiles to be enumerated.
            if (_range.Count <= _enumeratedTiles.Count)
            { 
                // no more tiles left.
                return false;
            }

            // try to move in the desired direction.
            Tile? next = null;
            while (next is null)
            { 
                // try until a valid tile is found.
                switch (_direction)
                {
                    case DirectionEnum.Up: // up
                        next = new Tile(_current.X, _current.Y - 1, _range.Zoom);
                        if (_enumeratedTiles.Contains(next))
                        { 
                            // moving up does not work, try to move left.
                            _direction = DirectionEnum.Left;
                            next = null;
                        }
                        else
                        { 
                            // moved up, try right.
                            _direction = DirectionEnum.Right;
                        }

                        break;
                    case DirectionEnum.Left: // left
                        next = new Tile(_current.X - 1, _current.Y, _range.Zoom);
                        if (_enumeratedTiles.Contains(next))
                        { 
                            // moving left does not work, try to move down.
                            _direction = DirectionEnum.Down;
                            next = null;
                        }
                        else
                        { // moved left, try up.
                            _direction = DirectionEnum.Up;
                        }

                        break;
                    case DirectionEnum.Down: // down
                        next = new Tile(_current.X, _current.Y + 1, _range.Zoom);
                        if (_enumeratedTiles.Contains(next))
                        { 
                            // moving down does not work, try to move right.
                            _direction = DirectionEnum.Right;
                            next = null;
                        }
                        else
                        { 
                            // moved down, try left.
                            _direction = DirectionEnum.Left;
                        }

                        break;
                    case DirectionEnum.Right: // right
                        next = new Tile(_current.X + 1, _current.Y, _range.Zoom);
                        if (_enumeratedTiles.Contains(next))
                        { 
                            // moving right does not work, try to move up.
                            _direction = DirectionEnum.Up;
                            next = null;
                        }
                        else
                        {
                            // moved right, try down.
                            _direction = DirectionEnum.Down;
                        }

                        break;
                }

                // test if the next is in range.
                if (next is not null && !_range.Contains(next))
                { // not in range, do not enumerate but move to next.
                    _current = next; // pretend the next has been enumerated.
                    next = null;
                }
            }

            // ok, next was found.
            _current = next;
            _enumeratedTiles.Add(_current);
            return true;
        }

        /// <summary>
        /// Resets this enumerator.
        /// </summary>
        public void Reset()
        {
            _current = null;
            _enumeratedTiles.Clear();
        }
    }

    /// <summary>
    /// Returns an enumerable that enumerates tiles with the center first.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Tile?> EnumerateInCenterFirst()
        => new TileRangeCenterFirst(this);

    /// <summary>
    /// Tile range center first enumerable.
    /// </summary>
    private sealed class TileRangeCenterFirst : IEnumerable<Tile?>
    {
        private readonly TileRange _tileRange;

        /// <summary>
        /// Creates a new range center first enumerable.
        /// </summary>
        /// <param name="tileRange"></param>
        public TileRangeCenterFirst(TileRange tileRange)
        {
            _tileRange = tileRange;
        }

        /// <summary>
        /// Returns the enumerator.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Tile?> GetEnumerator()
            => new TileRangeCenteredEnumerator(_tileRange);

        /// <summary>
        /// Returns the enumerator.
        /// </summary>
        /// <returns></returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => new TileRangeCenteredEnumerator(_tileRange);
    }
}