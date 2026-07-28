namespace Solace.BuildplateRenderer;

public sealed class MeshPrimitive
{
    public MeshPrimitive(IReadOnlyList<MeshVertex> vertices, IReadOnlyList<int> indices)
    {
        Vertices = vertices;
        Indices = indices;
    }

    public IReadOnlyList<MeshVertex> Vertices { get; } = [];

    public IReadOnlyList<int> Indices { get; } = [];

    public sealed class Builder
    {
        private List<MeshVertex> _vertices = [];

        private List<int> _indices = [];

        public int VertexCount => _vertices.Count;

        public void AddVertex(MeshVertex vertex)
            => _vertices.Add(vertex);

        public void AddIndex(int index)
            => _indices.Add(index);

        public MeshPrimitive Drain()
        {
            var vertices = _vertices;
            var indices = _indices;

            _vertices = [];
            _indices = [];

            return new MeshPrimitive(vertices, indices);
        }
    }
}
