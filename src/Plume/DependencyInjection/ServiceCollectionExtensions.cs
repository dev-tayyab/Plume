using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Plume.Abstractions;
using Plume.Resilience;

namespace Plume.DependencyInjection;

/// <summary>Extension methods to register Plume in an <see cref="IServiceCollection"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Register an <see cref="IPlumeClient"/> with the given configuration.</summary>
    public static IServiceCollection AddPlume(
        this IServiceCollection services,
        Action<PlumeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new PlumeOptions();
        configure(options);

        if (options.ProviderFactories.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one provider must be registered. " +
                "Call options.Use(...) inside AddPlume.");
        }

        services.TryAddSingleton(options);
        services.TryAddSingleton<IPlumeClient>(sp =>
        {
            var providers = options.ProviderFactories
                .Select(f => f(sp))
                .ToList();

            IProvider effective = providers.Count == 1
                ? providers[0]
                : new FailoverProvider(
                    providers,
                    sp.GetService<ILogger<FailoverProvider>>());

            return new DefaultPlumeClient(effective, options);
        });

        return services;
    }
}
