// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace idunno.Bluesky.BotSdk;

/// <summary>Configures the durable Bluesky bot runtime.</summary>
public sealed class BlueskyBotBuilder
{
    /// <summary>Initializes a new instance of the <see cref="BlueskyBotBuilder"/> class.</summary>
    /// <param name="services">The host service collection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public BlueskyBotBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>Gets the host service collection.</summary>
    /// <value>The host service collection.</value>
    public IServiceCollection Services { get; }

    /// <summary>Registers an application-defined action handler.</summary>
    /// <typeparam name="THandler">The action handler type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public BlueskyBotBuilder AddActionHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
        where THandler : class, IBotActionHandler
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotActionHandler, THandler>());
        return this;
    }

    /// <summary>Registers an application-defined action handler instance.</summary>
    /// <param name="handler">The action handler instance.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public BlueskyBotBuilder AddActionHandler(IBotActionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Services.AddSingleton(handler);
        return this;
    }
}
