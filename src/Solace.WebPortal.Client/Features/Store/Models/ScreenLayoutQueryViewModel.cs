namespace Solace.WebPortal.Client.Features.Store.Models;

public sealed class ScreenLayoutQueryViewModel
{
    public ColumnType ColumnType { get; set; } = ColumnType.Rectangle;

    public List<QueryViewModel> Queries { get; init; } = [];
}
