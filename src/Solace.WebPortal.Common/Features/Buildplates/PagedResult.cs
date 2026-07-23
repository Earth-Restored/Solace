namespace Solace.WebPortal.Common.Features.Buildplates;

public sealed record PagedResult<T>(List<T> Items, int TotalCount);
