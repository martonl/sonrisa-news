namespace NewsApp.Modules.Subscriptions;

public record UpdateSubscriptionRequest(string? Target, bool? IsActive);
