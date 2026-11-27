// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto.Space;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace idunno.AtProto.AspNet.Spaces;

/// <summary>
/// Adds ASP.NET Core hosting support for managed-space access checks.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public static class ManagedSpaceAccessEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Registers the managed-space access decider and its handler services.
    /// </summary>
    /// <typeparam name="TDecider">The application's managed-space access decider type.</typeparam>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="configureOptions">A callback that configures the managing application's service reference and token lifetime.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configureOptions"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddManagedSpaceAccess<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecider>(
        this IServiceCollection services,
        Action<ManagedSpaceAccessHandlerOptions> configureOptions)
        where TDecider : class, IManagedSpaceAccessDecider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<ManagedSpaceAccessHandlerOptions>()
            .Configure(configureOptions)
            .Validate(
                options => options.ManagingApp is not null,
                "The managing application service reference must be configured.")
            .Validate(
                options => options.MaximumTokenLifetime is null || options.MaximumTokenLifetime > TimeSpan.Zero,
                "The maximum service-token lifetime must be positive.")
            .ValidateOnStart();

        services.TryAddScoped<TDecider>();
        services.TryAddScoped<IManagedSpaceAccessDecider>(
            provider => provider.GetRequiredService<TDecider>());
        services.TryAddSingleton<IServiceTokenReplayGuard, InMemoryServiceTokenReplayGuard>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped(
            provider => new ManagedSpaceAccessHandler(
                options: provider.GetRequiredService<IOptions<ManagedSpaceAccessHandlerOptions>>().Value,
                decider: provider.GetRequiredService<IManagedSpaceAccessDecider>(),
                replayGuard: provider.GetRequiredService<IServiceTokenReplayGuard>(),
                timeProvider: provider.GetRequiredService<TimeProvider>()));

        return services;
    }

    /// <summary>
    /// Maps the <c>com.atproto.managedspace.checkAccess</c> XRPC endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The route handler builder for the mapped endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The endpoint requires HTTPS, rejects repeated query parameters and authorization headers, and marks responses
    /// as non-cacheable. The managing application's service entry in its DID document must point to the host's public
    /// service endpoint; the authority PDS appends this XRPC path and supplies the signed service token.</para>
    /// </remarks>
    public static IEndpointConventionBuilder MapManagedSpaceCheckAccess(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapGet(
                ManagedSpaceAccessHandler.Path,
                (RequestDelegate)HandleRequestAsync);
    }

    /// <summary>
    /// Validates the ASP.NET request before passing it to the framework-neutral handler.
    /// </summary>
    /// <param name="context">The HTTP request context.</param>
    /// <returns>A task that represents the endpoint operation.</returns>
    private static async Task HandleRequestAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";

        if (!context.Request.IsHttps)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status403Forbidden,
                "HttpsRequired",
                "HTTPS is required.").ConfigureAwait(false);
            return;
        }

        if (context.Request.Headers.Authorization.Count > 1 ||
            context.Request.Query.Any(parameter => parameter.Value.Count > 1))
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                "InvalidRequest",
                "Repeated request parameters are not allowed.").ConfigureAwait(false);
            return;
        }

        context.Request.Query.TryGetValue("space", out StringValues space);
        context.Request.Query.TryGetValue("repo", out StringValues repo);
        context.Request.Query.TryGetValue("clientId", out StringValues clientId);

        ManagedSpaceAccessHandler handler = context.RequestServices.GetRequiredService<ManagedSpaceAccessHandler>();
        ManagedSpaceAccessResponse response = await handler.HandleAsync(
            authorization: context.Request.Headers.Authorization.ToString(),
            space: space.Count == 0 ? null : space[0],
            repo: repo.Count == 0 ? null : repo[0],
            clientId: clientId.Count == 0 ? null : clientId[0],
            cancellationToken: context.RequestAborted).ConfigureAwait(false);

        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = ManagedSpaceAccessResponse.ContentType;
        await context.Response.Body.WriteAsync(
            response.Body,
            context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes an XRPC error response for a request rejected by the ASP.NET endpoint layer.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="error">The XRPC error name.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A task that represents the response write.</returns>
    private static Task WriteErrorAsync(HttpContext context, int statusCode, string error, string message)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("error", error);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = ManagedSpaceAccessResponse.ContentType;
        return context.Response.Body.WriteAsync(buffer.WrittenMemory, context.RequestAborted).AsTask();
    }

}
