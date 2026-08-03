using System.Globalization;
using System.Runtime.InteropServices;

namespace Solace.TileRenderer.Wkb;

[StructLayout(LayoutKind.Auto)]
internal readonly struct Point
{
    public double X { get; }
    public double Y { get; }

    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }

    public Point(BinaryReader reader)
    {
        this = Load(reader);
    }

    public static Point Load(BinaryReader reader)
        => new(reader.ReadDouble(), reader.ReadDouble());

    public static Point operator +(Point left, Point right)
        => new(left.X + right.X, left.Y + right.Y);

    public static Point operator -(Point left, Point right)
        => new(left.X - right.X, left.Y - right.Y);

    public static Point operator *(Point left, Point right)
        => new(left.X * right.X, left.Y * right.Y);

    public static Point operator /(Point left, Point right)
        => new(left.X / right.X, left.Y / right.Y);

    public static Point operator +(Point left, double right)
        => new(left.X + right, left.Y + right);

    public static Point operator -(Point left, double right)
        => new(left.X - right, left.Y - right);

    public static Point operator *(Point left, double right)
        => new(left.X * right, left.Y * right);

    public static Point operator /(Point left, double right)
        => new(left.X / right, left.Y / right);

    public override readonly string ToString()
        => $"<{X.ToString("G", CultureInfo.InvariantCulture)}, {Y.ToString("G", CultureInfo.InvariantCulture)}>";
}
