namespace NewsApp.Modules.NewsEvaluator;

public interface IRssFeedService
{
    Task<IReadOnlyList<RssItem>> FetchItemsAsync(CancellationToken ct = default);
}
