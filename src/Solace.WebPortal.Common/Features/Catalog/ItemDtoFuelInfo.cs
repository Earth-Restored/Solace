namespace Solace.WebPortal.Common.Features.Catalog;

public sealed record ItemDtoFuelInfo(
    int BurnTime,
    int HeatPerSecond,
    Guid? ReturnItemId
);
