using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Hosting;
using TMPublisher.Background.Localization;
using TMPublisher.Background.Notifications;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Composition;

/// <summary>The composition root of a run (<c>--run-due</c>): <see cref="SendingServices"/>, the run's log file, texts and notifications.</summary>
public static class BackgroundServices
{
    /// <summary>Builds the service provider.</summary>
    public static ServiceProvider Build(AppPaths paths, Action<IServiceCollection>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var services = new ServiceCollection();

        // The container makes (and on dispose closes) the log file.
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(paths.LogDirectory, LogKinds.Run));
        SendingServices.Add(services, paths);
        services.AddSingleton<TextCatalog>();
        services.AddSingleton<ILocalizer>(s => s.GetRequiredService<TextCatalog>());
        services.AddSingleton<NotifyProcess>();
        services.AddSingleton<INotifier, RunNotifier>();
        services.AddSingleton<RunStateFile>();
        services.AddSingleton<DueRun>();
        overrides?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}
