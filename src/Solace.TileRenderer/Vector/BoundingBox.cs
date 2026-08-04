using System.Runtime.InteropServices;

namespace Solace.TileRenderer.Vector;

// https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles/blob/develop/src/NetTopologySuite.IO.VectorTiles/Tiles/BoundingBox.cs

[StructLayout(LayoutKind.Auto)]
public readonly struct BoundingBox : IEquatable<BoundingBox>
{
    public BoundingBox(double left, double bottom, double right, double top)
    {
        Left = left;
        Bottom = bottom;
        Right = right;
        Top = top;
    }

    public double Left { get; }

    public double Bottom { get; }

    public double Right { get; }

    public double Top { get; }

    public override readonly bool Equals(object? obj)
        => obj is BoundingBox bounds && Equals(bounds);

    public readonly bool Equals(BoundingBox other)
        => Left == other.Left &&
            Bottom == other.Bottom &&
            Right == other.Right &&
            Top == other.Top;

    public override readonly int GetHashCode()
        => HashCode.Combine(Left, Bottom, Right, Top);

    public readonly double[] ToArray()
        => [Left, Bottom, Right, Top];

    public static bool operator ==(BoundingBox left, BoundingBox right)
        => left.Equals(right);

    public static bool operator !=(BoundingBox left, BoundingBox right)
        => !(left == right);
}