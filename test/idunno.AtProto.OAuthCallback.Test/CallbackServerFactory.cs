// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace idunno.AtProto.OAuthCallback.Test;

internal static class CallbackServerFactory
{
    internal const int MaximumAttempts = CallbackServer.MaximumCreationAttempts;

    internal static Task<CallbackServer> CreateAsync(
        string? path = null,
        ILoggerFactory? loggerFactory = null,
        Action<CallbackServer>? configure = null,
        Func<int>? getPort = null)
    {
        return CallbackServer.CreateAsync(
            path,
            loggerFactory,
            configure,
            getPort,
            TestContext.Current.CancellationToken);
    }
}
