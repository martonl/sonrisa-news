namespace NewsApp.Modules.Notifications;

public interface INewsQueue
{
    ValueTask EnqueueAsync(NewsNotification notification, CancellationToken ct = default);
    IAsyncEnumerable<NewsNotification> ReadAllAsync(CancellationToken ct = default);
}
