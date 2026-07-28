using System.Numerics;
using System.Runtime.InteropServices;

namespace Solace.WebPortal.Common.Features.Buildplates;

[StructLayout(LayoutKind.Auto)]
public readonly struct Bounds
{
    public Bounds()
    {
    }

    public Bounds(Vector3 min, Vector3 max)
    {
        MinX = min.X;
        MinY = min.Y;
        MinZ = min.Z;
        MaxX = max.X;
        MaxY = max.Y;
        MaxZ = max.Z;
    }

    public float MinX { get; init; }

    public float MinY { get; init; }

    public float MinZ { get; init; }

    public float MaxX { get; init; }

    public float MaxY { get; init; }

    public float MaxZ { get; init; }
}
