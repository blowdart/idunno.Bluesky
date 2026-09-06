// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using idunno.AtProto.Server;

namespace idunno.AtProto;

public partial class AtProtoAgent : Agent
{
    /// <summary>
    /// Describes the <paramref name="server"/>'s account creation requirements and capabilities.
    /// </summary>
    /// <param name="server">The service whose account description is to be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<AtProtoHttpResult<ServerDescription>> DescribeServer(Uri? server, CancellationToken cancellationToken = default)
    {
        server ??= Service;

        return await AtProtoServer.DescribeServer(server, HttpClient, LoggerFactory, cancellationToken).ConfigureAwait(false);
    }
}