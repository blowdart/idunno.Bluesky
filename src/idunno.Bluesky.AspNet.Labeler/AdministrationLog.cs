// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Logs administrative failure categories without echoing untrusted token or request input.</summary>
internal static partial class AdministrationLog
{
    /// <summary>Records a classified authentication, input or connection failure.</summary>
    [LoggerMessage(1, LogLevel.Warning, "Administrative request failed ({Category}).")]
    internal static partial void Rejected(ILogger logger, string category);
}
