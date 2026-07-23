using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Solace.WebPortal.Data;

public sealed class BuildplatePreviewEF
{
    public required Guid BuildplateId { get; set; }

    public required Guid PlayerId { get; set; }

    public required byte[] PreviewData { get; set; }

    [NotMapped, JsonIgnore]
    public bool IsTemplate => PlayerId == Guid.Empty;
}
