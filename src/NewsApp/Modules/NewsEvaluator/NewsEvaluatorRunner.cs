using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewsApp.Infrastructure.Data;
using NewsApp.Modules.Notifications;

namespace NewsApp.Modules.NewsEvaluator;

public sealed class NewsEvaluatorRunner(
    AppDbContext db,
    IRssFeedService rssFeedService,
    INewsEvaluatorAgent evaluatorAgent,
    INewsQueue queue,
    ILogger<NewsEvaluatorRunner> logger) : INewsEvaluatorRunner
{
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        // Load (or initialise) the single-row run-state record
        var state = await db.AgentRunStates.FindAsync([1], ct);
        bool isNew = state is null;
        state ??= new AgentRunState { Id = 1 };

        logger.LogInformation("News evaluator run started. LastRunAt={LastRunAt}", state.LastRunAt);

        // Fetch all RSS items then filter to only those published after the last run
        var allItems = await rssFeedService.FetchItemsAsync(ct);

        var newItems = state.LastRunAt.HasValue
            ? allItems.Where(i => i.PublishedAt > state.LastRunAt.Value).ToList()
            : (IReadOnlyList<RssItem>)allItems;

        if (newItems.Count == 0)
        {
            logger.LogInformation("No new RSS items since {LastRunAt}. Skipping AI evaluation.", state.LastRunAt);
            return;
        }

        logger.LogInformation("Sending {Count} new RSS items to AI agent.", newItems.Count);

        // Call the AI agent
        var notifications = await evaluatorAgent.EvaluateAsync(newItems, ct);

        // Enqueue each notification for the NotificationWorker
        foreach (var notification in notifications)
            await queue.EnqueueAsync(notification, ct);

        logger.LogInformation("Enqueued {Count} notifications.", notifications.Count);

        // Persist updated run state
        state.LastRunAt = DateTime.UtcNow;
        if (isNew)
            db.AgentRunStates.Add(state);

        await db.SaveChangesAsync(ct);
    }
}
