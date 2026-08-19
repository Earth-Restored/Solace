using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Client.Features.Store.Models;

public sealed class TabViewModel
{
    [Required] public string TabId { get; set; } = string.Empty;

    [Required] public string TabTitle { get; set; } = string.Empty;

    [Required] public string TabIcon { get; set; } = string.Empty;

    public List<ScreenLayoutQueryViewModel> ScreenLayoutQueries { get; init; } = [];

    public static TabViewModel FromDto(TabDto tab)
        => new()
        {
            TabId = tab.TabId,
            TabTitle = tab.TabTitle,
            TabIcon = tab.TabIcon,
            ScreenLayoutQueries = [.. tab.ScreenLayoutQueries.Select(sq => new ScreenLayoutQueryViewModel()
            {
                ColumnType = sq.ColumnType switch
                {
                    ColumnTypeDto.Rectangle => ColumnType.Rectangle,
                    ColumnTypeDto.Square => ColumnType.Square,
                    ColumnTypeDto.Grid => ColumnType.Grid,
                    _ => throw new UnreachableException(),
                },
                Queries = [.. sq.Queries.Select(q => new QueryViewModel()
                {
                    ProductIds = [.. q.ProductIds],
                })]
            })],
        };

    public TabDto ToDto()
        => new(
            TabId,
            TabTitle,
            TabIcon,
            ScreenLayoutQueries.Select(sq => new ScreenLayoutQueryDto(
                sq.ColumnType switch
                {
                    ColumnType.Rectangle => ColumnTypeDto.Rectangle,
                    ColumnType.Square => ColumnTypeDto.Square,
                    ColumnType.Grid => ColumnTypeDto.Grid,
                    _ => throw new UnreachableException(),
                },
                sq.Queries.Select(q => new QueryDto(
                    q.ProductIds
                ))
            ))
        );
}
