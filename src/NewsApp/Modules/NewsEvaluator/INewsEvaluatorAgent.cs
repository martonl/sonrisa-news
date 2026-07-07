using NewsApp.Modules.Notifications;

namespace NewsApp.Modules.NewsEvaluator;

public interface INewsEvaluatorAgent
{
    Task<IReadOnlyList<NewsNotification>> EvaluateAsync(
        IReadOnlyList<RssItem> items,
        CancellationToken ct = default);
}
