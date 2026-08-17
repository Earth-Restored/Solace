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
                ComponentId = sq.ComponentId,
                ColumnType = sq.ColumnType switch
                {
                    ColumnTypeDto.Rectangle => ColumnType.Rectangle,
                    ColumnTypeDto.Square => ColumnType.Square,
                    ColumnTypeDto.Grid => ColumnType.Grid,
                    _ => throw new UnreachableException(),
                },
                Queries = [.. sq.Queries.Select(q => new QueryViewModel()
                {
                    QueryContentTypes = [.. q.QueryContentTypes.Select(type => type switch
                    {
                        QueryContentTypeDto.Durable => QueryContentType.Durable,
                        QueryContentTypeDto.Collection => QueryContentType.Collection,
                        QueryContentTypeDto.Bundle => QueryContentType.Bundle,
                        QueryContentTypeDto.Persona => QueryContentType.Persona,
                        QueryContentTypeDto.Genoa => QueryContentType.Genoa,
                        QueryContentTypeDto.BuildplateOffer => QueryContentType.BuildplateOffer,
                        QueryContentTypeDto.RubyOffer => QueryContentType.RubyOffer,
                        QueryContentTypeDto.InventoryItemOffer => QueryContentType.InventoryItemOffer,
                        _ => throw new UnreachableException(),
                    })],
                    ProductIds = [.. q.ProductIds],
                })]
            })],
        };
}
