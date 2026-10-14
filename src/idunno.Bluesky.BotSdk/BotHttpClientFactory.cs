// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.BotSdk;

/// <summary>Routes the sender's SDK clients to its isolated, protected, durably accounted transport.</summary>
/// <param name="factory">The host's named HTTP client factory.</param>
internal sealed class BotHttpClientFactory(IHttpClientFactory factory) : IHttpClientFactory
{
    internal const string ClientName = "idunno.bluesky.bot";

    /// <inheritdoc/>
    public HttpClient CreateClient(string name) => factory.CreateClient(ClientName);
}
