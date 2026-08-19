namespace Solace.WebPortal.Common.Features.Data;

public sealed record GetSizeResponse(
    long EarthDb,
    long WebPortalDb,
    long PlayfabDb,
    long ObjectStore
);
