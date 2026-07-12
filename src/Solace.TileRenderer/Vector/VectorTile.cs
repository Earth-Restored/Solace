namespace Solace.TileRenderer.Vector;

// https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles/blob/develop/src/NetTopologySuite.IO.VectorTiles/VectorTile.cs

public sealed class VectorTile
{
    /// <summary>
    /// Gets or sets the tile id.
    /// </summary>
    public ulong TileId { get; set; }

    /// <summary>
    /// Gets or sets the layers.
    /// </summary>
    public IList<Layer> Layers { get; } = [];

    /// <summary>
    /// Gets the is empty flag.
    /// </summary>
    public bool IsEmpty => Layers == null || Layers.All(x => x.IsEmpty);
}