using System.Diagnostics;
using Solace.Common.Utils;

namespace Solace.DB.Models.Global;

public sealed class TemplateBuildplateEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required int Size { get; set; }

    public required int Offset { get; set; }

    public required int BlocksPerMeter { get; set; }

    public required bool Night { get; set; }

    public required Guid ServerDataObjectId { get; set; }

    public required Guid PreviewObjectId { get; set; }
}