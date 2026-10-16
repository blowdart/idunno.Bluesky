// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides middleware for publishing OAuth client metadata.
/// </summary>
public static class BlueskyOAuthClientMetadataExtensions
{
    /// <summary>
    /// Publishes the configured public web client's OAuth metadata document.
    /// </summary>
    /// <param name="app">The application pipeline.</param>
    /// <returns>The application pipeline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">OAuth options have not been configured.</exception>
    /// <exception cref="ArgumentException">The metadata configuration is invalid.</exception>
    /// <remarks>
    /// <para>Call this before authentication, authorization, static files and other middleware that might intercept the metadata URL.</para>
    /// <para>The document is generated and validated once when configuring the pipeline. Restart the application after changing its OAuth configuration.</para>
    /// <para>The path and query come from the configured client ID, including any application path base. Request headers never determine document contents.</para>
    /// </remarks>
    public static IApplicationBuilder UseBlueskyOAuthClientMetadata(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        OAuthOptions oAuthOptions =
            app.ApplicationServices.GetRequiredService<IOptionsMonitor<BlueskyAgentOptions>>().CurrentValue.OAuthOptions ??
            throw new InvalidOperationException("Configure BlueskyAgentOptions.OAuthOptions before publishing client metadata.");
        BlueskyOAuthClientMetadataOptions metadataOptions =
            app.ApplicationServices.GetRequiredService<IOptions<BlueskyOAuthClientMetadataOptions>>().Value;
        string document = metadataOptions.GenerateJson(oAuthOptions);
        Uri clientId = new(oAuthOptions.ClientId);
        PathString path = PathString.FromUriComponent(clientId);
        QueryString query = QueryString.FromUriComponent(clientId);
        int contentLength = Encoding.UTF8.GetByteCount(document);

        return app.Use(async (context, next) =>
        {
            if (!string.Equals(context.Request.PathBase.Add(context.Request.Path).Value, path.Value, StringComparison.Ordinal) ||
                !string.Equals(context.Request.QueryString.Value, query.Value, StringComparison.Ordinal))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                context.Response.Headers.Allow = "GET, HEAD";
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength = contentLength;
            if (HttpMethods.IsGet(context.Request.Method))
            {
                await context.Response.WriteAsync(document, context.RequestAborted).ConfigureAwait(false);
            }
        });
    }
}
