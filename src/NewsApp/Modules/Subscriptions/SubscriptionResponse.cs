namespace NewsApp.Modules.Subscriptions;

public record SubscriptionResponse(Guid Id, string UserId, SubscriptionType Type, string Target, bool IsActive, DateTime CreatedAt);
