// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace Samples.ReactBff;

internal static partial class BffLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF request failed with HTTP {Status}. Exception details are suppressed to protect OAuth credentials.")]
    internal static partial void RequestFailed(ILogger logger, int status);
}
