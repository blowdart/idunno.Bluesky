// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the current user's labeler declaration with its URI and CID for conditional updates.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The declaration record and its repository metadata.</returns>
    /// <exception cref="AuthenticationRequiredException">The current session is not authenticated.</exception>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The declaration and its envelope are registered in the Bluesky source generation context.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "The declaration and its envelope are registered in the Bluesky source generation context.")]
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Service>>> GetLabelerDeclarationRecord(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetRecord<Service>(
            new AtUri($"at://{Did}/{CollectionNsid.LabelerDeclaration}/self"),
            service: Service,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
