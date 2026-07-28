using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Solace.WebPortal.Common.Features.Catalog;

namespace Solace.WebPortal.Client.Features.Catalog;

public sealed class CatalogCacheService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public CatalogCacheService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<SyncAccessor> GetSyncAccessorAsync(CancellationToken cancellationToken = default)
        => new SyncAccessor(await GetData(cancellationToken));

    public async Task<ItemDto> GetItemAsync(Guid id, CancellationToken cancellationToken = default)
        => (await GetData(cancellationToken))[id];

    public async Task<IEnumerable<ItemDto>> SearchAsync(string search, CancellationToken cancellationToken = default)
    {
        var itemById = await GetData(cancellationToken);

        if (Guid.TryParse(search, out var id) && itemById.TryGetValue(id, out var item))
        {
            return [item];
        }

        return itemById
            .Values
            .Where(item => item.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<FrozenDictionary<Guid, ItemDto>> GetData(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var itemsById = await _cache.GetOrCreateAsync("Catalog_ItemsCatalog", async entry =>
            {
                // todo: make this configurable
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

                var items = await _httpClient.GetFromJsonAsync<IEnumerable<ItemDto>>("api/catalog/items");

                var itemsById = items!.ToFrozenDictionary(item => item.Id);

                return itemsById;
            });

        Debug.Assert(itemsById is not null);

        return itemsById;
    }

    public readonly struct SyncAccessor
    {
        private readonly FrozenDictionary<Guid, ItemDto> _itemById;

        public SyncAccessor(FrozenDictionary<Guid, ItemDto> itemById)
        {
            _itemById = itemById;
        }

        public bool TryGetItem(Guid id, [MaybeNullWhen(false)] out ItemDto item)
            => _itemById.TryGetValue(id, out item);

        public IEnumerable<ItemDto> Search(string search)
        {
            if (Guid.TryParse(search, out var id) && _itemById.TryGetValue(id, out var item))
            {
                return [item];
            }

            return _itemById
                .Values
                .Where(item => item.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        }
    }
}
