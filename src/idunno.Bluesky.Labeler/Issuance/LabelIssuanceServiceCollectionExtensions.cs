// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Registers host-neutral label issuance without installing authentication or a background worker.</summary>
public static class LabelIssuanceServiceCollectionExtensions
{
    /// <summary>Registers a scoped issuance service using caller-owned signing and HTTP dependencies.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="issuer">The bound labeler identity.</param>
    /// <param name="signingKey">The established caller-owned label key.</param>
    /// <param name="httpClient">The configured caller-owned HTTP client.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Issuance is already registered, or required dependencies are missing or ambiguous when resolved.</exception>
    /// <remarks><para>Register exactly one decision store, authorizer and policy provider. Registration neither
    /// initializes the database nor generates a key. The service does not dispose the supplied key/client.</para></remarks>
    public static IServiceCollection AddLabelIssuance(
        this IServiceCollection services,
        Did issuer,
        LabelSigningKey signingKey,
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(httpClient);
        if (services.Any(service => service.ServiceType == typeof(LabelIssuanceService)))
        {
            throw new InvalidOperationException("Label issuance is already registered.");
        }

        services.AddScoped(
            provider => new LabelIssuanceService(
                issuer: issuer,
                signingKey: signingKey,
                store: Single<ILabelStore>(provider),
                authorizer: Single<ILabelIssuanceAuthorizer>(provider),
                policyProvider: Single<ILabelIssuancePolicyProvider>(provider),
                httpClient: httpClient,
                timeProvider: provider.GetService<TimeProvider>(),
                loggerFactory: provider.GetService<ILoggerFactory>()));
        return services;
    }

    /// <summary>Rejects missing or ambiguous dependencies instead of silently selecting a registration.</summary>
    private static T Single<T>(IServiceProvider provider) where T : class
    {
        T[] instances = [.. provider.GetServices<T>()];
        return instances.Length == 1 ? instances[0] :
            throw new InvalidOperationException($"Register exactly one {typeof(T).Name}.");
    }
}
