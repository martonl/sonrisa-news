namespace NewsApp.Modules.Subscriptions;

public record CreateSubscriptionRequest(SubscriptionType Type, string Target);
