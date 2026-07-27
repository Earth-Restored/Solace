namespace Solace.WebPortal.Common.Features.Catalog;

public sealed record ItemDtoBoostInfo(
    string Name,
    int? Level,
    ItemDtoBoostInfoType Type,
    long Duration
);
