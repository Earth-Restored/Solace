namespace Solace.WebPortal.Common.Features.Store;

public sealed record TabDto(string TabId, string TabTitle, string TabIcon, IEnumerable<ScreenLayoutQueryDto> ScreenLayoutQueries);
