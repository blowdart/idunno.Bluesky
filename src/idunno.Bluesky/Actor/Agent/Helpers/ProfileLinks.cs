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
    /// Gets the hydrated links shown on the current user's profile, in display order.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The profile links, or the error details from the profile request.</returns>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<IReadOnlyCollection<ProfileLinkView>>> GetProfileLinks(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<ProfileViewDetailed> response = await GetProfile(Did, cancellationToken: cancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<ProfileLinkView>? links = response.Succeeded
            ? Array.AsReadOnly(response.Result.Links?.ToArray() ?? [])
            : null;

        return new AtProtoHttpResult<IReadOnlyCollection<ProfileLinkView>>(
            links,
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }

    /// <summary>
    /// Adds a link to the current user's profile.
    /// </summary>
    /// <param name="link">The link record to add.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The created link record, or the error details from a repository request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="link"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The profile already contains ten links.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The link record is created before updating the profile record. If the profile update fails, the link record may remain in the repository without being shown on the profile.</para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All record types are preserved in the source-generated serializer options passed to CreateRecord().")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All record types are preserved in the source-generated serializer options passed to CreateRecord().")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> AddProfileLink(
        Link link,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<AtProtoRepositoryRecord<Profile>> profileResponse =
            await GetProfileRecord(cancellationToken).ConfigureAwait(false);
        if (!profileResponse.Succeeded)
        {
            return new AtProtoHttpResult<CreateRecordResult>(
                null,
                profileResponse.StatusCode,
                profileResponse.HttpResponseHeaders,
                profileResponse.AtErrorDetail,
                profileResponse.RateLimit);
        }

        Profile profile = profileResponse.Result.Value;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(profile.Links?.Count ?? 0, 9);

        AtProtoHttpResult<CreateRecordResult> createResponse = await CreateRecord(
            record: link,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.Link,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!createResponse.Succeeded)
        {
            return createResponse;
        }

        profile.Links = profile.Links is null
            ? [createResponse.Result.StrongReference]
            : profile.Links.Append(createResponse.Result.StrongReference).ToArray();

        AtProtoHttpResult<PutRecordResult> updateResponse =
            await UpdateProfile(profileResponse.Result, cancellationToken).ConfigureAwait(false);

        return new AtProtoHttpResult<CreateRecordResult>(
            updateResponse.Succeeded ? createResponse.Result : null,
            updateResponse.StatusCode,
            updateResponse.HttpResponseHeaders,
            updateResponse.AtErrorDetail,
            updateResponse.RateLimit);
    }

    /// <summary>
    /// Removes a link from the current user's profile and deletes its repository record.
    /// </summary>
    /// <param name="link">A hydrated profile link to remove.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The result of deleting the link record, or the error details from a repository request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="link"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="link"/> does not refer to a link record in the current user's repository, is not present on the profile, or its CID does not match the profile reference.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>All profile references to the link are removed before the link record is deleted. If deleting the record fails, it will no longer appear on the profile but may remain in the repository.</para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All record types are preserved in the source-generated serializer options passed to UpdateProfile().")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All record types are preserved in the source-generated serializer options passed to UpdateProfile().")]
    public async Task<AtProtoHttpResult<DeleteResult>> RemoveProfileLink(
        ProfileLinkView link,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtUri uri = link.Uri;
        if (uri.Authority is not Did linkDid ||
            linkDid != Did ||
            uri.Collection != CollectionNsid.Link ||
            uri.RecordKey is null)
        {
            throw new ArgumentException("The link must refer to a link record in the current user's repository.", nameof(link));
        }

        AtProtoHttpResult<AtProtoRepositoryRecord<Profile>> profileResponse =
            await GetProfileRecord(cancellationToken).ConfigureAwait(false);
        if (!profileResponse.Succeeded)
        {
            return new AtProtoHttpResult<DeleteResult>(
                null,
                profileResponse.StatusCode,
                profileResponse.HttpResponseHeaders,
                profileResponse.AtErrorDetail,
                profileResponse.RateLimit);
        }

        Profile profile = profileResponse.Result.Value;
        StrongReference[] links = profile.Links?.ToArray() ?? [];
        StrongReference[] matchingLinks = [.. links.Where(existing => existing.Uri == uri)];
        if (matchingLinks.Length == 0)
        {
            throw new ArgumentException("The link is not present on the current user's profile.", nameof(link));
        }

        if (matchingLinks.Any(existing => existing.Cid != link.Cid))
        {
            throw new ArgumentException("The link CID does not match the profile reference.", nameof(link));
        }

        StrongReference profileLink = matchingLinks[0];
        profile.Links = [.. links.Where(existing => existing.Uri != uri)];
        AtProtoHttpResult<PutRecordResult> updateResponse =
            await UpdateProfile(profileResponse.Result, cancellationToken).ConfigureAwait(false);
        if (!updateResponse.Succeeded)
        {
            return new AtProtoHttpResult<DeleteResult>(
                null,
                updateResponse.StatusCode,
                updateResponse.HttpResponseHeaders,
                updateResponse.AtErrorDetail,
                updateResponse.RateLimit);
        }

        return await DeleteRecord(
            profileLink,
            swapRecord: profileLink.Cid,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
