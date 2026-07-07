using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NewsApp.Modules.NewsEvaluator;

public sealed class NewsSchedulerService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<NewsSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue<int>("NewsEvaluator:IntervalMinutes", 15);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        logger.LogInformation("NewsSchedulerService started. Interval: {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<INewsEvaluatorRunner>();
                await runner.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in NewsSchedulerService.");
            }

            await Task.Delay(interval, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        logger.LogInformation("NewsSchedulerService stopped.");
    }
}
