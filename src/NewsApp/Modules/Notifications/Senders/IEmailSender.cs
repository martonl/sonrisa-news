namespace NewsApp.Modules.Notifications.Senders;

public interface IEmailSender
{
    Task SendAsync(string to, NewsNotification notification, CancellationToken ct = default);
}
