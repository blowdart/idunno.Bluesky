// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authentication;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Helper functions to add the Bluesky authentication UI to an ASP.NET Core application.
/// </summary>
public static class BlueskyAuthenticationUIExtensions
{
    internal const string RequiresDynamicCodeMessage = "Using member 'Microsoft.Extensions.DependencyInjection.AddBlueskyAuthenticationUI(IServiceCollection)' which has 'RequiresDynamicCodeAttribute' can break functionality when trimming application code.Razor Pages does not currently support trimming or native AOT.https://aka.ms/aspnet/trimming.";
    internal const string TrimmingRequiredUnreferencedCodeMessage = "Using member 'Microsoft.Extensions.DependencyInjection.AddBlueskyAuthenticationUI(IServiceCollection)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code.Razor Pages does not currently support trimming or native AOT.https://aka.ms/aspnet/trimming.";

    /// <summary>
    /// Adds the Bluesky authentication UI to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/> to add the Bluesky authentication UI to.</param>
    /// <returns>The <see cref="AuthenticationBuilder"/> with the Bluesky authentication UI added.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null" />.</exception>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
    public static AuthenticationBuilder AddBlueskyAuthenticationUI(this AuthenticationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddRazorPages()
            .AddApplicationPart(typeof(BlueskyAuthenticationUIExtensions).Assembly);

        return builder;
    }
}
