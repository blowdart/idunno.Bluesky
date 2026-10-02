// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto.OAuthCallback.Test;

internal static class CallbackServerFactory
{
    internal const int MaximumAttempts = 5;

    internal static async Task<CallbackServer> CreateAsync(
        string? path = null,
        ILoggerFactory? loggerFactory = null,
        Action<CallbackServer>? configure = null,
        Func<int>? getPort = null)
    {
        getPort ??= CallbackServer.GetRandomUnusedPort;
        List<int> attemptedPorts = [];

        for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            int port = getPort();
            attemptedPorts.Add(port);
            CallbackServer server = new(port, path, loggerFactory);

            try
            {
                configure?.Invoke(server);
                await server.Startup.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

                return server;
            }
            catch (Exception exception) when (
                ReferenceEquals(server.Startup.Exception?.InnerException, exception) && IsAddressInUse(exception))
            {
                await server.DisposeAsync();

                if (attempt == MaximumAttempts)
                {
                    throw new InvalidOperationException(
                        $"Callback server startup failed after {MaximumAttempts} address collisions. Attempted ports: {string.Join(", ", attemptedPorts)}.",
                        exception);
                }
            }
            catch
            {
                await server.DisposeAsync();

                throw;
            }
        }

        throw new InvalidOperationException("Callback server startup attempts were exhausted.");
    }

    private static bool IsAddressInUse(Exception exception)
    {
        return exception is AddressInUseException ||
            (exception is AggregateException aggregate
                ? aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsAddressInUse)
                : exception.InnerException is not null && IsAddressInUse(exception.InnerException));
    }
}
