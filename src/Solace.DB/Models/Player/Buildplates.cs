using System.Text.Json.Serialization;
using Solace.Common.Utils;

namespace Solace.DB.Models.Player;

public sealed class LegacyBuildplates : IEquatable<LegacyBuildplates>
{
    [JsonInclude, JsonPropertyName("buildplates")]
    public Dictionary<string, Buildplate> Buildplates = [];

    public LegacyBuildplates()
    {
        // empty
    }

    public bool Equals(LegacyBuildplates? other)
        => other is not null &&
        Buildplates.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value)).SequenceEqual(other.Buildplates.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value)));

    public override bool Equals(object? obj)
        => Equals(obj as LegacyBuildplates);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var item in Buildplates.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            hash.Add(item.Key);
            hash.Add(item.Value);
        }

        return hash.ToHashCode();
    }

    public sealed class Vienna : IEquatable<Vienna>
    {
        [JsonInclude, JsonPropertyName("buildplates")]
        public Dictionary<string, Buildplate.Vienna> _buildplates = [];

        public Vienna()
        {
            // empty
        }

        public bool Equals(Vienna? other)
            => other is not null &&
            _buildplates.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value)).SequenceEqual(other._buildplates.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value)));

        public override bool Equals(object? obj)
            => Equals(obj as Vienna);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in _buildplates.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                hash.Add(item.Key);
                hash.Add(item.Value);
            }

            return hash.ToHashCode();
        }
    }

    public sealed record Buildplate(
        string? TemplateId,
        string Name,
        int Size,
        int Offset,
        int Scale,
        bool Night,
        long LastModified,
        string ServerDataObjectId,
        string PreviewObjectId
    )
    {
        public sealed record Vienna(
            int Size,
            int Offset,
            int Scale,
            bool Night,
            long LastModified,
            string ServerDataObjectId,
            string PreviewObjectId
        );
    }
}

public sealed class BuildplateEF : IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public string? TemplateId { get; set; }

    public required string Name { get; set; }

    public required int Size { get; set; }

    public required int Offset { get; set; }

    public required int Scale { get; set; }

    public required bool Night { get; set; }

    public required long LastModified { get; set; }

    public required string ServerDataObjectId { get; set; }

    public required string PreviewObjectId { get; set; }
}