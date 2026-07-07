using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NewsApp.Infrastructure.Data;
using NewsApp.Modules.NewsEvaluator;
using NewsApp.Modules.Notifications;

namespace NewsApp.Tests.AgentTests;

// ---------------------------------------------------------------------------
// Controllable test doubles
// ---------------------------------------------------------------------------

public sealed class FakeRssFeedService : IRssFeedService
{
    public IReadOnlyList<RssItem> Items { get; set; } = [];

    public Task<IReadOnlyList<RssItem>> FetchItemsAsync(CancellationToken ct = default)
        => Task.FromResult(Items);
}

public sealed class FakeNewsEvaluatorAgent : INewsEvaluatorAgent
{
    public IReadOnlyList<NewsNotification> Notifications { get; set; } = [];

    /// <summary>Records the items that were last passed for evaluation.</summary>
    public IReadOnlyList<RssItem> LastEvaluatedItems { get; private set; } = [];

    public int CallCount { get; private set; }

    public Task<IReadOnlyList<NewsNotification>> EvaluateAsync(
        IReadOnlyList<RssItem> items,
        CancellationToken ct = default)
    {
        LastEvaluatedItems = items;
        CallCount++;
        return Task.FromResult(Notifications);
    }
}

/// <summary>
/// A queue that records enqueued notifications but never delivers them
/// (so the NotificationWorker blocks harmlessly during tests).
/// </summary>
public sealed class RecordingNewsQueue : INewsQueue
{
    private readonly List<NewsNotification> _items = [];

    public IReadOnlyList<NewsNotification> Items => [.. _items];

    public ValueTask EnqueueAsync(NewsNotification notification, CancellationToken ct = default)
    {
        _items.Add(notification);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<NewsNotification> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Block forever so the NotificationWorker never tries to dispatch during tests.
        await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);
        yield break;
    }
}

// ---------------------------------------------------------------------------
// Test factory
// ---------------------------------------------------------------------------

public sealed class EvaluatorTestFactory : CustomWebApplicationFactory<Program>
{
    public FakeRssFeedService RssFeedService { get; } = new();
    public FakeNewsEvaluatorAgent EvaluatorAgent { get; } = new();
    public RecordingNewsQueue Queue { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            Replace<IRssFeedService>(services, RssFeedService);
            Replace<INewsEvaluatorAgent>(services, EvaluatorAgent);

            // Replace INewsQueue registrations with the recording queue
            services.RemoveAll<INewsQueue>();
            services.AddSingleton<INewsQueue>(Queue);
        });
    }

    private static void Replace<TService>(IServiceCollection services, TService implementation)
        where TService : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor is not null) services.Remove(descriptor);
        services.AddSingleton<TService>(implementation);
    }
}

// ---------------------------------------------------------------------------
// Integration tests
// ---------------------------------------------------------------------------

public class NewsSchedulerServiceTests : IAsyncLifetime
{
    private readonly EvaluatorTestFactory _factory = new();

    public Task InitializeAsync()
    {
        _ = _factory.Server; // Force host startup
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunOnce_WhenNoLastRunAt_PassesAllRssItemsToAgent()
    {
        // Arrange: no prior state, 3 RSS items
        _factory.RssFeedService.Items = MakeItems(3);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MakeAdminToken());

        // Act
        var response = await client.PostAsync("/api/admin/agent/run", null);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(3, _factory.EvaluatorAgent.LastEvaluatedItems.Count);
    }

    [Fact]
    public async Task RunOnce_FiltersOutItemsOlderThanLastRunAt()
    {
        // Arrange: seed a LastRunAt of 2 hours ago
        var cutoff = DateTime.UtcNow.AddHours(-2);
        await SeedAgentRunStateAsync(cutoff);

        // 2 items are newer than cutoff, 1 item is older
        _factory.RssFeedService.Items =
        [
            new RssItem("Old",     "...", "https://old.com",    cutoff.AddHours(-1)),
            new RssItem("Recent1", "...", "https://r1.com",     cutoff.AddHours(1)),
            new RssItem("Recent2", "...", "https://r2.com",     cutoff.AddMinutes(30)),
        ];

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MakeAdminToken());

        // Act
        var response = await client.PostAsync("/api/admin/agent/run", null);
        response.EnsureSuccessStatusCode();

        // Assert: only the 2 newer items were sent to the AI
        var evaluated = _factory.EvaluatorAgent.LastEvaluatedItems;
        Assert.Equal(2, evaluated.Count);
        Assert.All(evaluated, item => Assert.True(item.PublishedAt > cutoff));
    }

    [Fact]
    public async Task RunOnce_EnqueuesNotificationsReturnedByAgent()
    {
        // Arrange: agent returns 2 notifications
        _factory.RssFeedService.Items = MakeItems(2);
        _factory.EvaluatorAgent.Notifications =
        [
            new NewsNotification("Breaking", "Summary1", "https://n1.com", DateTime.UtcNow),
            new NewsNotification("Update",   "Summary2", "https://n2.com", DateTime.UtcNow),
        ];

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MakeAdminToken());

        // Act
        var response = await client.PostAsync("/api/admin/agent/run", null);
        response.EnsureSuccessStatusCode();

        // Assert: 2 items enqueued
        Assert.Equal(2, _factory.Queue.Items.Count);
        Assert.Equal("Breaking", _factory.Queue.Items[0].Title);
        Assert.Equal("Update",   _factory.Queue.Items[1].Title);
    }

    [Fact]
    public async Task RunOnce_UpdatesLastRunAtInDatabase()
    {
        // Arrange: no prior state
        _factory.RssFeedService.Items = MakeItems(1);
        _factory.EvaluatorAgent.Notifications = [new NewsNotification("N", "S", "https://x.com", DateTime.UtcNow)];

        var before = DateTime.UtcNow.AddSeconds(-1);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MakeAdminToken());

        // Act
        var response = await client.PostAsync("/api/admin/agent/run", null);
        response.EnsureSuccessStatusCode();

        // Assert: DB record created with recent LastRunAt
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.AgentRunStates.FindAsync([1]);

        Assert.NotNull(state);
        Assert.NotNull(state.LastRunAt);
        Assert.True(state.LastRunAt > before);
    }

    [Fact]
    public async Task RunOnce_WhenAllItemsOlderThanLastRunAt_DoesNotCallAgent()
    {
        // Arrange: all RSS items are older than LastRunAt
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        await SeedAgentRunStateAsync(cutoff);

        _factory.RssFeedService.Items =
        [
            new RssItem("Old1", "...", "https://a.com", cutoff.AddHours(-2)),
            new RssItem("Old2", "...", "https://b.com", cutoff.AddHours(-1)),
        ];

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MakeAdminToken());

        // Act
        var response = await client.PostAsync("/api/admin/agent/run", null);
        response.EnsureSuccessStatusCode();

        // Assert: agent was never called
        Assert.Equal(0, _factory.EvaluatorAgent.CallCount);
        Assert.Empty(_factory.Queue.Items);
    }

    [Fact]
    public async Task AdminAgentRunEndpoint_RequiresAuthentication()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/admin/agent/run", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminAgentRunEndpoint_RequiresAdminRole()
    {
        var client = _factory.CreateClient();
        // Token with no roles
        var token = TestJwtHelper.GenerateToken(Guid.NewGuid().ToString(), "user@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/admin/agent/run", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static IReadOnlyList<RssItem> MakeItems(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new RssItem($"Title {i}", "Summary", $"https://news.com/{i}", DateTime.UtcNow))
            .ToList();

    private static string MakeAdminToken() =>
        TestJwtHelper.GenerateToken(Guid.NewGuid().ToString(), "admin@test.com", ["Admin"]);

    private async Task SeedAgentRunStateAsync(DateTime lastRunAt)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AgentRunStates.Add(new AgentRunState { Id = 1, LastRunAt = lastRunAt });
        await db.SaveChangesAsync();
    }
}
