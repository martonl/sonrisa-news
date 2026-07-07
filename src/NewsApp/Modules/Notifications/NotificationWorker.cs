using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewsApp.Infrastructure.Data;
using NewsApp.Modules.Notifications.Senders;
using NewsApp.Modules.Subscriptions;

namespace NewsApp.Modules.Notifications;

public sealed class NotificationWorker(
    INewsQueue queue,
    IServiceScopeFactory scopeFactory,
    IEmailSender emailSender,
    ISlackSender slackSender,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationWorker started.");

        await foreach (var notification in queue.ReadAllAsync(stoppingToken))
        {
            await DispatchNotificationAsync(notification, stoppingToken);
        }

        logger.LogInformation("NotificationWorker stopping.");
    }

    private async Task DispatchNotificationAsync(NewsNotification notification, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subscriptions = await db.Subscriptions
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        foreach (var sub in subscriptions)
        {
            try
            {
                if (sub.Type == SubscriptionType.Email)
                    await emailSender.SendAsync(sub.Target, notification, ct);
                else if (sub.Type == SubscriptionType.Slack)
                    await slackSender.SendAsync(sub.Target, notification, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send notification to {Target}", sub.Target);
            }
        }
    }
}
