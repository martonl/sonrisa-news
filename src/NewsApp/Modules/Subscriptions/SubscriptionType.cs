using System.Text.Json.Serialization;

namespace NewsApp.Modules.Subscriptions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionType { Email, Slack }
