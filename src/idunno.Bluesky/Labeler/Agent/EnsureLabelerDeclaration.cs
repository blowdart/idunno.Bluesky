// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates the current user's labeler declaration if it does not exist, without replacing a different declaration.
    /// </summary>
    /// <param name="declaration">The declaration to publish.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The current declaration record if it matches, or the newly created record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="declaration"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The declaration contains invalid label policies or report scopes.</exception>
    /// <exception cref="AuthenticationRequiredException">The current session is not authenticated.</exception>
    /// <remarks>
    /// <para>A matching existing record is returned unchanged. A different existing declaration is reported as a conflict and is never overwritten. A failed read or create is returned through the result.</para>
    /// <para>This publishes only <c>app.bsky.labeler.service/self</c>; it does not publish a signing key or labeler endpoint in the DID document.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Service>>> EnsureLabelerDeclaration(
        Service declaration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        LabelerDeclarationValidator.Validate(declaration);

        AtProtoHttpResult<AtProtoRepositoryRecord<Service>> current = await GetLabelerDeclarationRecord(
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (current.Succeeded)
        {
            return DeclarationsMatch(current.Result.Value, declaration)
                ? current
                : DeclarationConflict(current);
        }

        if (current.StatusCode != HttpStatusCode.NotFound &&
            current.AtErrorDetail?.Error != "RecordNotFound")
        {
            return current;
        }

        AtProtoHttpResult<CreateRecordResult> create = await CreateLabelerDeclaration(
            declaration: declaration,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        bool alreadyExists = create.StatusCode == HttpStatusCode.Conflict ||
            create.AtErrorDetail?.Error == "RecordAlreadyExists";
        if (!create.Succeeded && !alreadyExists)
        {
            return new AtProtoHttpResult<AtProtoRepositoryRecord<Service>>(
                result: null,
                statusCode: create.StatusCode,
                httpResponseHeaders: create.HttpResponseHeaders,
                atErrorDetail: create.AtErrorDetail,
                rateLimit: create.RateLimit);
        }

        current = await GetLabelerDeclarationRecord(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (current.Succeeded)
        {
            return DeclarationsMatch(current.Result.Value, declaration)
                ? current
                : DeclarationConflict(current);
        }

        if (!create.Succeeded && alreadyExists)
        {
            return new AtProtoHttpResult<AtProtoRepositoryRecord<Service>>(
                result: null,
                statusCode: create.StatusCode,
                httpResponseHeaders: create.HttpResponseHeaders,
                atErrorDetail: create.AtErrorDetail,
                rateLimit: create.RateLimit);
        }

        return current;
    }

    private static bool DeclarationsMatch(Service existing, Service requested)
    {
        byte[] existingJson = JsonSerializer.SerializeToUtf8Bytes(existing, SourceGenerationContext.Default.LabelerService);
        byte[] requestedJson = JsonSerializer.SerializeToUtf8Bytes(requested, SourceGenerationContext.Default.LabelerService);
        return existingJson.AsSpan().SequenceEqual(requestedJson);
    }

    private static AtProtoHttpResult<AtProtoRepositoryRecord<Service>> DeclarationConflict(
        AtProtoHttpResult<AtProtoRepositoryRecord<Service>> current) =>
        new(
            result: null,
            statusCode: HttpStatusCode.Conflict,
            httpResponseHeaders: current.HttpResponseHeaders,
            atErrorDetail: new AtErrorDetail("DeclarationConflict", "A different labeler declaration already exists."),
            rateLimit: current.RateLimit);
}
