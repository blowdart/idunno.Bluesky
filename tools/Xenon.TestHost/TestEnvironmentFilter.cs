// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Xenon.TestHost;

/// <summary>Marks every Xenon page as a fixture-backed development environment.</summary>
internal sealed class TestEnvironmentFilter : IAsyncPageFilter
{
    /// <inheritdoc/>
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (context.HandlerInstance is PageModel page)
        {
            page.ViewData["EnvironmentNotice"] =
                "LOCAL TEST ENVIRONMENT - fictional moderation data, real Bluesky sign-in. No public label distribution.";
        }
        await next();
    }
}
