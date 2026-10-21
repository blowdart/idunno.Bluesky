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
    /// Replaces the current user's labeler declaration only if its CID matches the expected version.
    /// </summary>
    /// <param name="declaration">The replacement declaration.</param>
    /// <param name="swapRecord">The CID of the declaration being replaced.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The result of updating the declaration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="declaration"/> or <paramref name="swapRecord"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The declaration contains invalid label policies or report scopes.</exception>
    /// <exception cref="AuthenticationRequiredException">The current session is not authenticated.</exception>
    /// <remarks>
    /// <para>Read the original with <see cref="GetLabelerDeclarationRecord(CancellationToken)"/> and use a
    /// <c>with</c> expression to retain its creation time, policies, report scopes, and extension data.
    /// A concurrent change is returned as an <c>InvalidSwap</c> error; it is not retried or overwritten.</para>
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The declaration is registered in the Bluesky source generation context.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "The declaration is registered in the Bluesky source generation context.")]
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateLabelerDeclaration(
        Service declaration,
        Cid swapRecord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(swapRecord);
        LabelerDeclarationValidator.Validate(declaration);

        return await PutRecord(
            record: declaration,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.LabelerDeclaration,
            rKey: "self",
            swapRecord: swapRecord,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
