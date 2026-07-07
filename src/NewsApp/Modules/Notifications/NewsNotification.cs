namespace NewsApp.Modules.Notifications;

public record NewsNotification(
    string Title,
    string Summary,
    string Url,
    DateTime PublishedAt);
