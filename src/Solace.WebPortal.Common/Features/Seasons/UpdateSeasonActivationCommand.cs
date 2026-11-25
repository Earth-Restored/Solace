namespace Solace.WebPortal.Common.Features.Seasons;

public sealed record UpdateSeasonActivationCommand(
    bool IsActive,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate
);
