using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace NewsApp.Modules.Notifications;

public sealed class NewsChannel : INewsQueue
{
    private readonly Channel<NewsNotification> _channel =
        Channel.CreateUnbounded<NewsNotification>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(NewsNotification notification, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(notification, ct);

    public async IAsyncEnumerable<NewsNotification> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var notification in _channel.Reader.ReadAllAsync(ct))
            yield return notification;
    }
}
