using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Plume.Abstractions;
using Plume.Resilience;

namespace Plume.DependencyInjection;

/// <summary>Extension methods to register Plume embeddings in an <see cref="IServiceCollection"/>.</summary>
public static class EmbeddingsServiceCollectionExtensions
{
    /// <summary>Register an <see cref="IEmbeddingClient"/> with the given configuration.</summary>
    public static IServiceCollection AddPlumeEmbeddings(
        this IServiceCollection services,
        Action<PlumeEmbeddingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new PlumeEmbeddingOptions();
        configure(options);

        if (options.ProviderFactories.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one embedding provider must be registered. " +
                "Call options.Use(...) inside AddPlumeEmbeddings.");
        }

        services.TryAddSingleton(options);
        services.TryAddSingleton<IEmbeddingClient>(sp =>
        {
            var providers = options.ProviderFactories
                .Select(f => f(sp))
                .ToList();

            IEmbeddingProvider effective = providers.Count == 1
                ? providers[0]
                : new FailoverEmbeddingProvider(
                    providers,
                    sp.GetService<ILogger<FailoverEmbeddingProvider>>());

            return new DefaultEmbeddingClient(effective, options);
        });

        return services;
    }
}
