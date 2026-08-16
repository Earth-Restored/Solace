namespace Solace.WebPortal.Common.Features.Shop;

public sealed record TabDto(string TabId, string TabTitle, string TabIcon, IEnumerable<ScreenLayoutQueryDto> ScreenLayoutQueries);
