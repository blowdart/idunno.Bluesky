// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;
using idunno.AtProto;

using idunno.Bluesky.Record;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Gets the post record for the specified <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference" /> of the post to return the <see cref="PostRecord"/> for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="strongReference"/> is null.</exception>
        public async Task<AtProtoHttpResult<PostRecord>> GetPostRecord(
            StrongReference strongReference,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            return await BlueskyServer.GetPost(strongReference, AuthenticatedOrUnauthenticatedServiceUri, AccessToken, HttpClient, cancellationToken).ConfigureAwait(false);
        }
    }
}
