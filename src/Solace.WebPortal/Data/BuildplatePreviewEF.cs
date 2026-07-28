using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Solace.WebPortal.Data;

public sealed class BuildplatePreviewEF
{
    public required Guid BuildplateId { get; set; }

    public required Guid PlayerId { get; set; }

    public required byte[] PreviewData { get; set; }

    public float BoundsMinX { get; set; }
    public float BoundsMinY { get; set; }
    public float BoundsMinZ { get; set; }

    public float BoundsMaxX { get; set; }
    public float BoundsMaxY { get; set; }
    public float BoundsMaxZ { get; set; }

    [NotMapped, JsonIgnore]
    public required Vector3 BoundsMin
    {
        get => new(BoundsMinX, BoundsMinY, BoundsMinZ);
        set
        {
            BoundsMinX = value.Z;
            BoundsMinY = value.Y;
            BoundsMinZ = value.Z;
        }
    }

    [NotMapped, JsonIgnore]
    public required Vector3 BoundsMax
    {
        get => new(BoundsMaxX, BoundsMaxY, BoundsMaxZ);
        set
        {
            BoundsMaxX = value.Z;
            BoundsMaxY = value.Y;
            BoundsMaxZ = value.Z;
        }
    }

    [NotMapped, JsonIgnore]
    public bool IsTemplate => PlayerId == Guid.Empty;
}
