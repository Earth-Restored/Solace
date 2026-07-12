namespace Solace.TileRenderer.Vector;

// https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles/blob/develop/src/NetTopologySuite.IO.VectorTiles.Mapbox/MapboxCommandType.cs

/// <summary>
/// <a href="https://github.com/mapbox/vector-tile-spec/tree/master/2.1#433-command-types">Command Types</a>
/// </summary>
public enum MapboxCommandType
{
    /// <summary>
    /// <a href="https://github.com/mapbox/vector-tile-spec/tree/master/2.1#4331-moveto-command">MoveTo Command</a>
    /// </summary>
    MoveTo = 1,

    /// <summary>
    /// <a href="https://github.com/mapbox/vector-tile-spec/tree/master/2.1#4332-lineto-command">LineTo Command</a>
    /// </summary>
    LineTo = 2,

    /// <summary>
    /// <a href="https://github.com/mapbox/vector-tile-spec/tree/master/2.1#4333-closepath-command">ClosePath Command</a>
    /// </summary>
    ClosePath = 7,
}