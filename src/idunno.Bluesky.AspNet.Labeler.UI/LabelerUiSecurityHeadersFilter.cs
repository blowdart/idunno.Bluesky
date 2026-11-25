// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Restricts browser capabilities on rendered Xenon and authentication HTML without changing protocol responses.</summary>
/// <param name="allowOAuthNavigation">Whether forms on this page initiate navigation to a discovered HTTPS authorization server.</param>
internal sealed class LabelerUiSecurityHeadersFilter(bool allowOAuthNavigation) : IResultFilter
{
    /// <inheritdoc/>
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is PageResult or ViewResult or PartialViewResult)
        {
            // Browsers also check form-action against POST redirects. Only OAuth-entry pages need
            // HTTPS destinations that cannot be known until the submitted account is resolved.
            string formDestinations = allowOAuthNavigation ? "'self' https:" : "'self'";
            context.HttpContext.Response.Headers.ContentSecurityPolicy =
                "default-src 'none'; script-src 'self'; style-src 'self'; " +
                "img-src 'self' data: https:; font-src 'self'; media-src 'self'; " +
                "connect-src 'self' https://public.api.bsky.app; " +
                "frame-ancestors 'none'; base-uri 'none'; object-src 'none'; " +
                $"form-action {formDestinations}";
            IHeaderDictionary headers = context.HttpContext.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            if (!headers.ContainsKey("Referrer-Policy"))
            {
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            }

            headers["Permissions-Policy"] =
                "accelerometer=(), camera=(), geolocation=(), gyroscope=(), microphone=(), payment=(), usb=()";

            // Antiforgery token generation adds X-Frame-Options: SAMEORIGIN while the page renders; replace it
            // just before headers are sent so it agrees with the CSP frame-ancestors 'none'.
            context.HttpContext.Response.OnStarting(
                static state =>
                {
                    ((HttpContext)state).Response.Headers.XFrameOptions = "DENY";
                    return Task.CompletedTask;
                },
                context.HttpContext);
        }
    }

    /// <inheritdoc/>
    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
