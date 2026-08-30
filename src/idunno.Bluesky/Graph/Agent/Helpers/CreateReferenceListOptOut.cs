// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a reference list opt out record for the given list, requesting that its author be omitted from the public presentation of a reference list.
    /// </summary>
    /// <param name="subject">Canonical, <see cref="Did"/>-based <see cref="AtUri"/> of the app.bsky.graph.list record from which the author requests omission.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when the provided subject is not a valid list record.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateReferenceListOptOut(AtUri subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (!Did.TryParse(subject.Repo.Value, out _))
        {
            throw new ArgumentException("subject repo is not a DID", nameof(subject));
        }

        if (subject.Collection != CollectionNsid.List)
        {
            throw new ArgumentException($"subject does not point to an {CollectionNsid.List} record", nameof(subject));
        }

        var record = new ReferenceListOptOut(subject);

        return await CreateBlueskyRecord(
            record,
            collection: CollectionNsid.ReferenceListOptOut,
            rKey: TimestampIdentifier.Next(),
            validate: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
