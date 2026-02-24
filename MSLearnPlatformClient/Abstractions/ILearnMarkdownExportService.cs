using MSLearnPlatformClient.Models;
using MSLearnPlatformClient.Models.Catalog;

namespace MSLearnPlatformClient.Abstractions;

public interface ILearnMarkdownExportService
{
    Task<IReadOnlyList<LearnMarkdownDocument>> ExportAsync(CatalogQuery query, CancellationToken cancellationToken);
}
