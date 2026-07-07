using Microsoft.Extensions.Logging;

namespace NewsApp.Modules.Notifications.Senders;

public sealed class StubSlackSender(ILogger<StubSlackSender> logger) : ISlackSender
{
    public Task SendAsync(string channel, NewsNotification notification, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[SLACK] Channel: {Channel} | Title: {Title} | Url: {Url}",
            channel, notification.Title, notification.Url);
        return Task.CompletedTask;
    }
}
