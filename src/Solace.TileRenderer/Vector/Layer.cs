using NetTopologySuite.Features;

namespace Solace.TileRenderer.Vector;

// https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles/blob/develop/src/NetTopologySuite.IO.VectorTiles/Layer.cs

public class Layer
{
    /// <summary>
    /// Gets or sets the name of the layer.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the is empty flag.
    /// </summary>
    public virtual bool IsEmpty => Features is null or { Count: 0 };

    /// <summary>
    /// Gets the features.
    /// </summary>
    public IList<IFeature> Features { get; } = [];
}