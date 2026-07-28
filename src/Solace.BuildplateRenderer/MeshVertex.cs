using System.Numerics;
using System.Runtime.InteropServices;

namespace Solace.BuildplateRenderer;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MeshVertex
{
    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector2 UV;
    // public readonly int TintIndex;

    public MeshVertex(Vector3 position, Vector3 normal, Vector2 uV, int tintIndex)
    {
        Position = position;
        Normal = normal;
        UV = uV;
        _ = tintIndex;
        // TintIndex = tintIndex;
    }
}
