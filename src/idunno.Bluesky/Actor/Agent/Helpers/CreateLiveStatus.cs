// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a profile status for the current authenticated user to indicate a live stream.
    /// </summary>
    /// <param name="uri">The uri of the live stream.</param>
    /// <param name="title">The title of the stream.</param>
    /// <param name="description">A description of the stream.</param>
    /// <param name="previewBlob">An optional <see cref="Blob"/> containing a preview image for the stream.</param>
    /// <param name="durationMinutes">The optional duration of the stream in minutes.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateLiveStatus(
        Uri uri,
        string title,
        string description,
        Blob? previewBlob = null,
        int? durationMinutes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        Status status = new(
            KnownStatusValues.Live,
            embed: new EmbeddedExternal(
                uri: uri,
                title: title,
                description: description,
                thumbnail: previewBlob),
            durationMinutes: durationMinutes,
            createdAt: DateTimeOffset.UtcNow);

        return await CreateStatus(
            status: status,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
