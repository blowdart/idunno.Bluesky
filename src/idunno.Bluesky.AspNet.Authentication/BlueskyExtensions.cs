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
    /// <summary>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the default scheme.
    /// The default scheme is specified by <see cref="BlueskyAuthenticationDefaults.AuthenticationScheme"/>.
    /// <para>
    /// Bluesky authentication uses a combination of OAuth and a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    public static AuthenticationBuilder AddBluesky(this AuthenticationBuilder builder)
        => builder.AddBluesky(BlueskyAuthenticationDefaults.AuthenticationScheme);

    /// <summary>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the specified scheme.
    /// <para>
    /// Bluesky authentication uses a combination of OAuth and a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    public static AuthenticationBuilder AddBluesky(this AuthenticationBuilder builder, string authenticationScheme)
        => builder.AddBluesky(authenticationScheme, configureOptions: null!);

    /// <summary>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the default scheme.
    /// The default scheme is specified by <see cref="BlueskyAuthenticationDefaults.AuthenticationScheme"/>.
    /// <para>
    /// Bluesky authentication uses a combination of OAuth and a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="BlueskyAuthenticationOptions"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    public static AuthenticationBuilder AddBluesky(this AuthenticationBuilder builder, Action<BlueskyAuthenticationOptions> configureOptions)
        => builder.AddBluesky(BlueskyAuthenticationDefaults.AuthenticationScheme, configureOptions);

    /// <summary>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the specified scheme.
    /// <para>
    /// Bluesky authentication uses a combination of OAuth and a HTTP cookie persisted in the client to perform authentication.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="BlueskyAuthenticationOptions"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    public static AuthenticationBuilder AddBluesky(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        Action<BlueskyAuthenticationOptions> configureOptions)
            => builder.AddBluesky(authenticationScheme, displayName: null, configureOptions: configureOptions);

    /// <summary>
    /// Adds Bluesky authentication to <see cref="AuthenticationBuilder"/> using the specified scheme.
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
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "Options class is capture in JSON source generation.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "Options class is capture in JSON source generation.")]
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
        builder.Services.TryAddSingleton<IBlueskyAuthenticationCorrelationCache, BlueskyAuthenticationCorrelationCache>();
        builder.Services.TryAddScoped<BlueskySignInManager, BlueskySignInManager>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<BlueskyAuthenticationOptions>, PostConfigureBlueskyAuthenticationOptions>());
        builder.Services.AddOptions<BlueskyAuthenticationOptions>(authenticationScheme).Validate(
            o => o.Cookie.Expiration == null, "BlueskyAuthenticationOptions.Expiration is ignored, use ExpireTimeSpan instead.");

        builder.Services.AddScoped<BlueskyAgentFactory>();
        builder.Services.AddScoped(s => s.GetRequiredService<BlueskyAgentFactory>().CreateBlueskyAgent());

        return builder.AddScheme<BlueskyAuthenticationOptions, BlueskyAuthenticationHandler>(authenticationScheme, displayName, configureOptions);
    }

    /// <summary>
    /// Adds the <see cref="ProfileClaimsTransformer"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>/</param>
    /// <param name="configureOptions">A delegate to configure <see cref="ProfileClaimsTransformerOptions"/>.</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddProfileClaimsTransformer(
        this IServiceCollection services,
        Action<ProfileClaimsTransformerOptions> configureOptions)
    {
        services.AddTransient<IClaimsTransformation, ProfileClaimsTransformer>();
        services.Configure(configureOptions);

        return services;
    }
}
