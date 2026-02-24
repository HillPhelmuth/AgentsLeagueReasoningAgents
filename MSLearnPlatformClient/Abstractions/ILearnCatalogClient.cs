using MSLearnPlatformClient.Models.Catalog;

namespace MSLearnPlatformClient.Abstractions;

public interface ILearnCatalogClient
{
    Task<CatalogResponse> QueryCatalogAsync(CatalogQuery query, CancellationToken cancellationToken = default);
    Task<CatalogResponse> GetCatalogItemAsync(CatalogItemType type, string uid, CancellationToken cancellationToken = default);
    Task<CatalogResponse> GetCatalogItemsAsync(CatalogItemType type, IEnumerable<string> uids, CancellationToken cancellationToken = default);
}
