namespace Solace.WebPortal.Client.Features.Seasons.Models;

public sealed record SeasonTextureSlot(
    string Category,
    string FileName,
    string DisplayName,
    string ExpectedSize,
    string Description
);
