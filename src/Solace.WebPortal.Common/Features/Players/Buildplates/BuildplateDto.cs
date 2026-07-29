namespace Solace.WebPortal.Common.Features.Players.Buildplates;

public sealed record BuildplateDto(
    Guid Id,
    Guid? TemplateId,
    string Name,
    int BlocksPerMeter,
    int Size,
    int Offset,
    bool IsNight,
    Guid ServerDataObjectId,
    Guid PreviewObjectId
);
