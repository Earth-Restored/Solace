namespace Solace.WebPortal.Common.Features.Buildplates;

public sealed record BuildplateTemplateDto(
    Guid Id,
    string Name,
    int BlocksPerMeter,
    int Size,
    int Offset,
    bool IsNight,
    Guid ServerDataObjectId,
    Guid PreviewObjectId
);
