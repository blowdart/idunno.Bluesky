// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using idunno.AtProto.Server;

using Microsoft.Extensions.Caching.Memory;

namespace idunno.AtProto;

public partial class AtProtoAgent : Agent
{
    private readonly MemoryCacheEntryOptions _serverDescriptionCacheEntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(5),
        Size = 1
    };

    /// <summary>
    /// Describes the <paramref name="server"/>'s account creation requirements and capabilities.
    /// </summary>
    /// <param name="server">The service whose account description is to be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks><para>Caching descriptions can be controlled by <see cref="AtProtoAgentOptions.CacheServerDescriptions"/> and <see cref="AtProtoAgentOptions.ServerDescriptionCacheSize"/>.</para></remarks>
    public async Task<AtProtoHttpResult<ServerDescription>> DescribeServer(Uri? server, CancellationToken cancellationToken = default)
    {
        server ??= Service;

        if (_serverDescriptionCache is not null && _serverDescriptionCache.TryGetValue(server.Host, out AtProtoHttpResult<ServerDescription>? cachedDescription) && cachedDescription is not null)
        {
            return cachedDescription;
        }

        AtProtoHttpResult<ServerDescription> result = await AtProtoServer.DescribeServer(server, HttpClient, LoggerFactory, cancellationToken).ConfigureAwait(false);

        if (result.Succeeded && _serverDescriptionCache is not null)
        {
            _serverDescriptionCache.Set(server.Host, result, _serverDescriptionCacheEntryOptions);
        }

        return result;
    }
}