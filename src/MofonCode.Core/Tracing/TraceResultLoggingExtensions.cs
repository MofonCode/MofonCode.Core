using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MofonCode.Core;

/// <summary>Extension methods for registering TraceResult logging and observability in the DI container.</summary>
/// <remarks>
/// For projects using Aspire service defaults, <c>AddTraceResultLogging</c> is called automatically
/// by <c>AddServiceDefaults()</c>. Only call these methods directly when the service defaults
/// pipeline is not used (e.g., Azure Functions isolated worker).
/// </remarks>
public static class TraceResultLoggingExtensions
{
    /// <summary>Registers <see cref="TraceResultOptions"/> in the service collection.</summary>
    public static IServiceCollection AddTraceResultLogging(
        this IServiceCollection services,
        Action<TraceResultOptions>? configure = null)
    {
        TraceResultOptions options = new();
        configure?.Invoke(options);
        services.AddSingleton(options);
        return services;
    }

    /// <summary>
    /// Activates TraceResult logging by wiring FluentResults to the application's
    /// <see cref="ILoggerFactory"/> and initializing the <see cref="System.Diagnostics.ActivitySource"/>
    /// and <see cref="System.Diagnostics.Metrics.Meter"/> from the registered <see cref="TraceResultOptions"/>.
    /// </summary>
    public static IServiceProvider UseTraceResultLogging(this IServiceProvider serviceProvider)
    {
        ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        TraceResultOptions options = serviceProvider.GetService<TraceResultOptions>() ?? new TraceResultOptions();
        FluentResultLogging.Initialize(loggerFactory, options);
        return serviceProvider;
    }
}
