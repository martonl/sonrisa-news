namespace NewsApp.Modules.Notifications.Senders;

public interface ISlackSender
{
    Task SendAsync(string channel, NewsNotification notification, CancellationToken ct = default);
}
