namespace Solace.WebPortal.Common.Features.Store;

public sealed record TabSummaryDto(string TabId, HashSet<Guid>? Items);
