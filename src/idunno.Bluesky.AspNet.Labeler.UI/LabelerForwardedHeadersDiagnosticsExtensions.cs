// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Labeler.UI;

using Microsoft.AspNetCore.Http;

#pragma warning disable IDE0130 // Middleware extensions use the ASP.NET Core builder namespace.
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>Provides request capture for the Xenon forwarded-headers diagnostic page.</summary>
public static class LabelerForwardedHeadersDiagnosticsExtensions
{
    /// <summary>Captures selected request values for the Xenon forwarded-headers diagnostic page.</summary>
    /// <param name="app">The application pipeline.</param>
    /// <returns>The application pipeline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Call this after any path-base middleware and before ASP.NET Core forwarded-header middleware.</para>
    /// <para>Only GET requests for the diagnostic page are captured. Values are untrusted and remain scoped to the request.</para>
    /// </remarks>
    public static IApplicationBuilder UseLabelerForwardedHeadersDiagnostics(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use((context, next) =>
        {
            string? path = context.Request.Path.Value;
            if (HttpMethods.IsGet(context.Request.Method) &&
                (string.Equals(path, "/Xenon/ForwardedHeaders", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(path, "/Xenon/ForwardedHeaders/", StringComparison.OrdinalIgnoreCase)) &&
                !context.Items.ContainsKey(typeof(ForwardedHeadersSnapshot)))
            {
                context.Items[typeof(ForwardedHeadersSnapshot)] = ForwardedHeadersSnapshot.Capture(context);
            }

            return next(context);
        });
    }
}
