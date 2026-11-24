// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Captures the selected proxy-related request values at one stage of the pipeline.</summary>
/// <param name="Scheme">The request scheme at capture time.</param>
/// <param name="RemoteIpAddress">The connection's remote address at capture time.</param>
/// <param name="Headers">The standard header names and values at capture time.</param>
/// <param name="HostingHeaders">The hosting-specific header names and values at capture time.</param>
internal sealed record ForwardedHeadersSnapshot(
    string Scheme,
    string? RemoteIpAddress,
    IReadOnlyList<KeyValuePair<string, string?>> Headers,
    IReadOnlyList<KeyValuePair<string, string?>> HostingHeaders)
{
    /// <summary>Copies only the fields displayed by the forwarded-headers diagnostic page.</summary>
    /// <param name="context">The current request context.</param>
    /// <returns>The request values at capture time.</returns>
    internal static ForwardedHeadersSnapshot Capture(HttpContext context)
    {
        return new ForwardedHeadersSnapshot(
            Scheme: context.Request.Scheme,
            RemoteIpAddress: context.Connection.RemoteIpAddress?.ToString(),
            Headers:
            [
                Header(context, "X-Forwarded-For"),
                Header(context, "X-Forwarded-Proto"),
                Header(context, "X-Original-For"),
                Header(context, "X-Original-Proto")
            ],
            HostingHeaders:
            [
                Header(context, "CF-Connecting-IP"),
                Header(context, "X-Azure-ClientIP"),
                Header(context, "X-Azure-SocketIP"),
                Header(context, "X-Azure-Ref"),
                Header(context, "X-Azure-RequestChain"),
                Header(context, "X-Azure-FDID")
            ]);
    }

    /// <summary>Reads one header without conflating an absent header with an empty value.</summary>
    /// <param name="context">The current request context.</param>
    /// <param name="name">The header name.</param>
    /// <returns>The header name and its value, or null when absent.</returns>
    private static KeyValuePair<string, string?> Header(HttpContext context, string name)
    {
        return new KeyValuePair<string, string?>(
            name,
            context.Request.Headers.TryGetValue(name, out StringValues values) ? values.ToString() : null);
    }
}
