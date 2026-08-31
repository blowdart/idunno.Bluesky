// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a <see cref="Profile"/> for the current user.
    /// </summary>
    /// <param name="profile">The <see cref="Profile"/> to create.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateProfile(
        Profile profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await CreateBlueskyRecord(
            record: profile,
            collection: CollectionNsid.Profile,
            rKey: "self",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a <see cref="Profile"/> for the current user.
    /// </summary>
    /// <param name="profile">The <see cref="Profile"/> to create.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateProfile(
        Profile profile)
    {
        return await CreateProfile(profile, default).ConfigureAwait(false);
    }
}
