using Microsoft.Extensions.DependencyInjection;
using NewsApp.Modules.Notifications.Senders;

namespace NewsApp.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddSingleton<NewsChannel>();
        services.AddSingleton<INewsQueue>(sp => sp.GetRequiredService<NewsChannel>());
        services.AddSingleton<IEmailSender, StubEmailSender>();
        services.AddSingleton<ISlackSender, StubSlackSender>();
        services.AddHostedService<NotificationWorker>();
        return services;
    }
}
