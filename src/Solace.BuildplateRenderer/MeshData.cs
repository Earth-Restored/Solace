using System.Numerics;
using BitcoderCZ.Maths.Vectors;

namespace Solace.BuildplateRenderer;

public sealed class MeshData
{
    public MeshData(IReadOnlyDictionary<string, MeshPrimitive> primitives, Vector3 boundsMin, Vector3 boundsMax)
    {
        Primitives = primitives;
        BoundsMin = boundsMin;
        BoundsMax = boundsMax;
    }

    // Grouped by texture
    public IReadOnlyDictionary<string, MeshPrimitive> Primitives { get; }

    public Vector3 BoundsMin { get; }

    public Vector3 BoundsMax { get; }

    public sealed class Builder
    {
        private Dictionary<string, MeshPrimitive.Builder> _primitives = [];

        private int3 _boundsMin = new(int.MaxValue);

        private int3 _boundsMax = new(int.MinValue);

        public void RegisterBlock(int3 position)
        {
            _boundsMin = int3.Min(_boundsMin, position);
            _boundsMax = int3.Max(_boundsMax, position);
        }

        public MeshPrimitive.Builder GetPrimitive(string texture)
        {
            if (!_primitives.TryGetValue(texture, out var primitive))
            {
                primitive = new MeshPrimitive.Builder();
                _primitives[texture] = primitive;
            }

            return primitive;
        }

        public MeshData Drain()
        {
            var primitives = _primitives;
            var boundsMin = _boundsMin;
            var boundsMax = _boundsMax;

            _primitives = [];
            _boundsMin = new(int.MaxValue);
            _boundsMax = new(int.MinValue);

            return new MeshData(primitives.ToDictionary(item => item.Key, item => item.Value.Drain(), StringComparer.Ordinal), boundsMin, boundsMax);
        }
    }
}
