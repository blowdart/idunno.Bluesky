// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates the current user's labeler declaration without replacing an existing declaration.
    /// </summary>
    /// <param name="declaration">The declaration to publish.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The result of creating the declaration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="declaration"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The declaration contains invalid label policies or report scopes.</exception>
    /// <exception cref="AuthenticationRequiredException">The current session is not authenticated.</exception>
    /// <remarks>
    /// <para>The record is created at <c>app.bsky.labeler.service/self</c>. Repository errors, including an
    /// existing record, are returned through the result. This does not register a signing key or service endpoint.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateLabelerDeclaration(
        Service declaration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        LabelerDeclarationValidator.Validate(declaration);

        return await CreateBlueskyRecord(
            declaration,
            CollectionNsid.LabelerDeclaration,
            rKey: "self",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
