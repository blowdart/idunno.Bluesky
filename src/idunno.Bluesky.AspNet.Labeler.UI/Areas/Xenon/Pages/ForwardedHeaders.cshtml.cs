// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages;

/// <summary>Shows how forwarded headers reached this host so proxy trust settings can be checked. It has no navigation link and requires a signed-in team member.</summary>
public sealed class ForwardedHeadersModel : PageModel
{
    /// <summary>Gets the request values captured before forwarded-header processing, when available.</summary>
    internal ForwardedHeadersSnapshot? Received { get; private set; }

    /// <summary>Gets the request scheme after any forwarded headers were applied.</summary>
    internal string Scheme { get; private set; } = string.Empty;

    /// <summary>Gets the client address after any forwarded headers were applied.</summary>
    internal string? RemoteIpAddress { get; private set; }

    /// <summary>Gets the standard header values keyed by name, with null for headers that are absent.</summary>
    internal IReadOnlyList<KeyValuePair<string, string?>> Headers { get; private set; } = [];

    /// <summary>Gets the hosting-specific header values keyed by name, with null for headers that are absent.</summary>
    internal IReadOnlyList<KeyValuePair<string, string?>> HostingHeaders { get; private set; } = [];

    /// <summary>Captures the proxy-related request state without exposing cookies, tokens or other headers.</summary>
    public void OnGet()
    {
        Response.Headers.CacheControl = "private, no-store";
        if (HttpContext.Items.TryGetValue(typeof(ForwardedHeadersSnapshot), out object? value) &&
            value is ForwardedHeadersSnapshot received)
        {
            Received = received;
        }

        ForwardedHeadersSnapshot effective = ForwardedHeadersSnapshot.Capture(HttpContext);
        Scheme = effective.Scheme;
        RemoteIpAddress = effective.RemoteIpAddress;
        Headers = effective.Headers;
        HostingHeaders = effective.HostingHeaders;
    }

    /// <summary>Distinguishes missing and empty header values in the diagnostic display.</summary>
    /// <param name="value">The captured header value.</param>
    /// <returns>The value or an explanatory placeholder.</returns>
    internal static string DisplayHeader(string? value)
    {
        return value switch
        {
            null => "(not present)",
            "" => "(empty)",
            _ => value
        };
    }
}
