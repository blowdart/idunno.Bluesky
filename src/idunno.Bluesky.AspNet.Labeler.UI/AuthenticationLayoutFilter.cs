// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Uses the Xenon shell for the authentication pages only when Xenon is registered.</summary>
internal sealed class AuthenticationLayoutFilter : IResultFilter
{
    /// <inheritdoc/>
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is PageResult page)
        {
            page.ViewData["AuthenticationLayout"] = "/Areas/Xenon/Pages/_Layout.cshtml";
        }
    }

    /// <inheritdoc/>
    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
