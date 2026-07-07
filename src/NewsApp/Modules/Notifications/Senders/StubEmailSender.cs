using Microsoft.Extensions.Logging;

namespace NewsApp.Modules.Notifications.Senders;

public sealed class StubEmailSender(ILogger<StubEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, NewsNotification notification, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EMAIL] To: {To} | Title: {Title} | Url: {Url}",
            to, notification.Title, notification.Url);
        return Task.CompletedTask;
    }
}
