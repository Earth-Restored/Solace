namespace Solace.WebPortal.Client.Features.Store.Models;

public sealed class ScreenLayoutQueryViewModel
{
    public Guid ComponentId { get; set; } = Guid.NewGuid();

    public ColumnType ColumnType { get; set; } = ColumnType.Rectangle;

    public List<QueryViewModel> Queries { get; init; } = [];
}
