using System.ComponentModel.DataAnnotations;

namespace Solace.WebPortal.Client.Features.Shop;

public sealed class TabViewModel
{
    [Required] public string TabId { get; set; } = string.Empty;

    [Required] public string TabTitle { get; set; } = string.Empty;

    public string TabIcon { get; set; } = string.Empty;

    public List<ScreenLayoutQueryViewModel> ScreenLayoutQueries { get; init; } = [];
}
