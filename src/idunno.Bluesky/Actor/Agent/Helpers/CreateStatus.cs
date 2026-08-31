// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a profile status for the current authenticated user.
    /// </summary>
    /// <param name="status">The status to set</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="status"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateStatus(
        Status status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await CreateBlueskyRecord(
            record: status,
            collection: CollectionNsid.Status,
            rKey: "self",
            validate: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
