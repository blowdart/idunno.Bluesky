// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Helper functions to add Bluesky authentication capabilities to an HTTP application pipeline.
/// </summary>
public static class BlueskyExtensions
{
    internal const string RequiresDynamicCodeMessage = "Binding strongly typed objects to configuration values may require generating dynamic code at runtime.";
    internal const string TrimmingRequiredUnreferencedCodeMessage = "BlueskyAgentOptions instances may their members trimmed. Ensure all required members are preserved.";

    /// <summary>
    ///<para>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the default scheme.
    /// The default scheme is specified by <see cref="BlueskyAuthenticationDefaults.AuthenticationScheme"/>.
    /// </para>
    /// <para>
    /// Bluesky authentication uses a combination of OAuth and a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
    public static AuthenticationBuilder AddBluesky(this AuthenticationBuilder builder)
        => builder.AddBluesky(BlueskyAuthenticationDefaults.AuthenticationScheme);

    /// <summary>
    /// <para>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the specified scheme.
    /// </para>
    /// <para>
    /// Bluesky authentication uses a combination of OAuth and a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
    public static AuthenticationBuilder AddBluesky(this AuthenticationBuilder builder, string authenticationScheme)
        => builder.AddBluesky(authenticationScheme, configureOptions: null!);

    /// <summary>
    /// <para>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the default scheme.
    /// The default scheme is specified by <see cref="BlueskyAuthenticationDefaults.AuthenticationScheme"/>.
    /// </para>
    /// <para>
    /// Bluesky authentication uses a combination of OAuth and a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="BlueskyAuthenticationOptions"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
    public static AuthenticationBuilder AddBluesky(this AuthenticationBuilder builder, Action<BlueskyAuthenticationOptions> configureOptions)
        => builder.AddBluesky(BlueskyAuthenticationDefaults.AuthenticationScheme, configureOptions);

    /// <summary>
    /// <para>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the specified scheme.
    /// </para>
    /// <para>
    /// Bluesky authentication uses a combination of OAuth and a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="BlueskyAuthenticationOptions"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
    public static AuthenticationBuilder AddBluesky(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        Action<BlueskyAuthenticationOptions> configureOptions)
            => builder.AddBluesky(authenticationScheme, displayName: null, configureOptions: configureOptions);

    /// <summary>
    /// <para>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the specified scheme.
    /// </para>
    /// <para>
    /// Bluesky authentication uses a combination of OAuth and a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <param name="displayName">A display name for the authentication handler.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="BlueskyAuthenticationOptions"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> is <see langword="null"/>.</exception>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
    public static AuthenticationBuilder AddBluesky(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        string? displayName,
        Action<BlueskyAuthenticationOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureOptions);

        builder.Services.AddBlueskyAgentOptions();
        builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        builder.Services.TryAddScoped<BlueskySignInManager, BlueskySignInManager>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<BlueskyAuthenticationOptions>, PostConfigureBlueskyAuthenticationOptions>());
        builder.Services.AddOptions<BlueskyAuthenticationOptions>(authenticationScheme).Validate(
            o => o.Cookie.Expiration == null, "BlueskyAuthenticationOptions.Expiration is ignored, use ExpireTimeSpan instead.");

        return builder.AddScheme<BlueskyAuthenticationOptions, BlueskyAuthenticationHandler>(authenticationScheme, displayName, configureOptions);
    }

    /// <summary>
    /// Adds the <see cref="ProfileClaimsTransformer"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>/</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddProfileClaimsTransformer(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<ProfileClaimsTransformerOptions>, PostConfigureProfileClaimsTransformerOptions>());
        services.AddOptions<ProfileClaimsTransformerOptions>();
        services.AddTransient<IClaimsTransformation, ProfileClaimsTransformer>();

        return services;
    }

    /// <summary>
    /// Adds a <see cref="BlueskyAgentFactory"/> and to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add to.</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddBlueskyAgentFactory(this IServiceCollection services)
    {
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddOptions<BlueskyAgentOptions>();
        services.AddSingleton<BlueskyAgentFactory>();
        services.AddScoped(provider => provider.GetRequiredService<BlueskyAgentFactory>().CreateAgent());

        return services;
    }
}
