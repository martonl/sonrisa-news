using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NewsApp.Infrastructure.Data;
using NewsApp.Modules.Identity;
using NewsApp.Modules.Notifications;
using NewsApp.Modules.Notifications.Senders;
using NewsApp.Modules.Subscriptions;

namespace NewsApp.Tests.NotificationTests;

// ---------------------------------------------------------------------------
// Recording test doubles
// ---------------------------------------------------------------------------

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentBag<(string To, NewsNotification Notification)> _calls = [];
    private readonly SemaphoreSlim _signal = new(0);

    public IReadOnlyList<(string To, NewsNotification Notification)> Calls => [.. _calls];

    public Task SendAsync(string to, NewsNotification notification, CancellationToken ct = default)
    {
        _calls.Add((to, notification));
        _signal.Release();
        return Task.CompletedTask;
    }

    /// <summary>Waits until at least one call arrives or the token is cancelled.</summary>
    public Task WaitForCallAsync(CancellationToken ct) => _signal.WaitAsync(ct);
}

public sealed class RecordingSlackSender : ISlackSender
{
    private readonly ConcurrentBag<(string Channel, NewsNotification Notification)> _calls = [];
    private readonly SemaphoreSlim _signal = new(0);

    public IReadOnlyList<(string Channel, NewsNotification Notification)> Calls => [.. _calls];

    public Task SendAsync(string channel, NewsNotification notification, CancellationToken ct = default)
    {
        _calls.Add((channel, notification));
        _signal.Release();
        return Task.CompletedTask;
    }

    /// <summary>Waits until at least one call arrives or the token is cancelled.</summary>
    public Task WaitForCallAsync(CancellationToken ct) => _signal.WaitAsync(ct);
}

// ---------------------------------------------------------------------------
// Factory that replaces stub senders with recording ones
// ---------------------------------------------------------------------------

public sealed class NotificationTestFactory : CustomWebApplicationFactory<Program>
{
    public RecordingEmailSender EmailSender { get; } = new();
    public RecordingSlackSender SlackSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var emailDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailSender));
            if (emailDesc is not null) services.Remove(emailDesc);
            services.AddSingleton<IEmailSender>(EmailSender);

            var slackDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ISlackSender));
            if (slackDesc is not null) services.Remove(slackDesc);
            services.AddSingleton<ISlackSender>(SlackSender);
        });
    }
}

// ---------------------------------------------------------------------------
// Integration tests
// ---------------------------------------------------------------------------

/// <summary>
/// Each test method gets its own factory (and therefore its own in-memory DB
/// and fresh recording senders) because xunit creates a new test-class instance
/// per method and IAsyncLifetime disposes the factory after each method.
/// </summary>
public class NotificationWorkerTests : IAsyncLifetime
{
    private readonly NotificationTestFactory _factory = new();

    public Task InitializeAsync()
    {
        // Accessing Server forces the host to build and start all background services.
        _ = _factory.Server;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Worker_WhenActiveEmailSubscriptionExists_DispatchesEmailNotification()
    {
        // Arrange: seed an active email subscription
        const string emailTarget = "subscriber@example.com";
        await SeedSubscriptionAsync(SubscriptionType.Email, emailTarget, isActive: true);

        var queue = _factory.Services.GetRequiredService<INewsQueue>();
        var notification = new NewsNotification("Breaking", "Big event", "https://news.example.com", DateTime.UtcNow);

        // Act
        await queue.EnqueueAsync(notification);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _factory.EmailSender.WaitForCallAsync(cts.Token);

        // Assert
        var calls = _factory.EmailSender.Calls;
        Assert.Single(calls);
        Assert.Equal(emailTarget, calls[0].To);
        Assert.Equal(notification.Title, calls[0].Notification.Title);
        Assert.Equal(notification.Url, calls[0].Notification.Url);
    }

    [Fact]
    public async Task Worker_WhenActiveSlackSubscriptionExists_DispatchesSlackNotification()
    {
        // Arrange: seed an active Slack subscription
        const string slackChannel = "#alerts";
        await SeedSubscriptionAsync(SubscriptionType.Slack, slackChannel, isActive: true);

        var queue = _factory.Services.GetRequiredService<INewsQueue>();
        var notification = new NewsNotification("Market Update", "Stocks fell 5%", "https://finance.example.com", DateTime.UtcNow);

        // Act
        await queue.EnqueueAsync(notification);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _factory.SlackSender.WaitForCallAsync(cts.Token);

        // Assert
        var calls = _factory.SlackSender.Calls;
        Assert.Single(calls);
        Assert.Equal(slackChannel, calls[0].Channel);
        Assert.Equal(notification.Title, calls[0].Notification.Title);
    }

    [Fact]
    public async Task Worker_WhenSubscriptionIsInactive_DoesNotDispatch()
    {
        // Arrange: seed an INACTIVE subscription — worker must skip it
        await SeedSubscriptionAsync(SubscriptionType.Email, "inactive@example.com", isActive: false);

        var queue = _factory.Services.GetRequiredService<INewsQueue>();
        await queue.EnqueueAsync(new NewsNotification("Ignored", "No one cares", "https://x.com", DateTime.UtcNow));

        // Give the worker enough time to process the notification (if it were going to dispatch it would be fast)
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        try
        {
            await _factory.EmailSender.WaitForCallAsync(cts.Token);
            // If we reach here the sender was unexpectedly called
            Assert.Fail("Email sender should not have been called for inactive subscription.");
        }
        catch (OperationCanceledException)
        {
            // Expected — no dispatch occurred within the timeout window
        }

        Assert.Empty(_factory.EmailSender.Calls);
    }

    [Fact]
    public async Task Worker_WithMixedSubscriptions_DispatchesBothEmailAndSlack()
    {
        // Arrange: one email + one slack subscription
        const string emailTarget = "mixed-email@example.com";
        const string slackTarget = "#mixed-slack";
        await SeedSubscriptionAsync(SubscriptionType.Email, emailTarget, isActive: true);
        await SeedSubscriptionAsync(SubscriptionType.Slack, slackTarget, isActive: true);

        var queue = _factory.Services.GetRequiredService<INewsQueue>();
        var notification = new NewsNotification("Disaster", "Earthquake", "https://news.com/eq", DateTime.UtcNow);

        // Act
        await queue.EnqueueAsync(notification);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(
            _factory.EmailSender.WaitForCallAsync(cts.Token),
            _factory.SlackSender.WaitForCallAsync(cts.Token));

        // Assert
        Assert.Single(_factory.EmailSender.Calls);
        Assert.Equal(emailTarget, _factory.EmailSender.Calls[0].To);

        Assert.Single(_factory.SlackSender.Calls);
        Assert.Equal(slackTarget, _factory.SlackSender.Calls[0].Channel);
    }

    // -----------------------------------------------------------------------
    // Queue unit-level tests (no DB / no worker needed)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NewsChannel_EnqueueAsync_ItemIsReadable()
    {
        var channel = new NewsChannel();
        var notification = new NewsNotification("Test", "Details", "https://t.com", DateTime.UtcNow);

        await channel.EnqueueAsync(notification);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var received = await channel.ReadAllAsync(cts.Token).FirstAsync();

        Assert.Equal(notification.Title, received.Title);
        Assert.Equal(notification.Url, received.Url);
    }

    [Fact]
    public async Task NewsChannel_MultipleEnqueuedItems_AreReadInOrder()
    {
        var channel = new NewsChannel();
        var items = Enumerable.Range(1, 5)
            .Select(i => new NewsNotification($"Title {i}", "Summary", $"https://t.com/{i}", DateTime.UtcNow))
            .ToList();

        foreach (var item in items)
            await channel.EnqueueAsync(item);

        var received = new List<NewsNotification>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        try
        {
            await foreach (var n in channel.ReadAllAsync(cts.Token))
            {
                received.Add(n);
                if (received.Count == items.Count) break;
            }
        }
        catch (OperationCanceledException) { /* reading done */ }

        Assert.Equal(items.Count, received.Count);
        for (int i = 0; i < items.Count; i++)
            Assert.Equal(items[i].Title, received[i].Title);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a real user (to satisfy the FK) and seeds the subscription within
    /// the same scope so both operations share the same DbContext instance.
    /// </summary>
    private async Task SeedSubscriptionAsync(SubscriptionType type, string target, bool isActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var email = $"sub-{Guid.NewGuid()}@test.com";
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, "Password123!");
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = type,
            Target = target,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
