using System.Diagnostics;

namespace Solace.TappablesGenerator;

public static class TileUtils
{
    public static int XYToInt(int x, int y)
    {
        Debug.Assert(x is >= 0 and < ushort.MaxValue);
        Debug.Assert(y is >= 0 and < ushort.MaxValue);

        return (x << 16) + y;
    }
}
