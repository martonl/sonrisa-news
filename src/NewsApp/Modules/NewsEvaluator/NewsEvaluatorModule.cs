using Microsoft.Extensions.DependencyInjection;

namespace NewsApp.Modules.NewsEvaluator;

public static class NewsEvaluatorModule
{
    public static IServiceCollection AddNewsEvaluatorModule(this IServiceCollection services)
    {
        services.AddHttpClient<IRssFeedService, RssFeedService>();
        services.AddScoped<INewsEvaluatorAgent, NewsEvaluatorAgent>();
        services.AddScoped<INewsEvaluatorRunner, NewsEvaluatorRunner>();
        services.AddHostedService<NewsSchedulerService>();
        return services;
    }

    public static IEndpointRouteBuilder MapNewsEvaluatorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/agent/run",
            async (INewsEvaluatorRunner runner, CancellationToken ct) =>
            {
                await runner.RunOnceAsync(ct);
                return Results.Ok(new { message = "Agent run completed." });
            })
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        return app;
    }
}
