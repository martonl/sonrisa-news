using NewsApp.Modules.Identity;

namespace NewsApp.Modules.Subscriptions;

public class Subscription
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public SubscriptionType Type { get; set; }
    public string Target { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
