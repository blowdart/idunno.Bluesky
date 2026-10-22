// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler;

/// <summary>Resolves issuance targets through the existing identity and repository clients.</summary>
/// <remarks><para>Resolution verifies identity and record location, not an application's lexicon schema.
/// Withdrawals never call this component: the stored DID and signed scope remain usable when the subject
/// has been deleted or its PDS is unavailable.</para></remarks>
internal sealed class LabelTargetResolver(HttpClient httpClient, ILoggerFactory loggerFactory)
{
    /// <summary>Verifies identity and, for records, checks the requested version at its discovered PDS.</summary>
    /// <remarks><para>An omitted CID remains omitted in the returned target: fetching a record proves its
    /// existence at lookup time, but must not silently change a URI-wide decision into a pinned one.</para></remarks>
    internal async Task<LabelServiceResult<LabelTarget>> ResolveAsync(LabelTarget target, CancellationToken token)
    {
        try
        {
            Did? did = target.Repository as Did;
            if (target.Repository is Handle handle)
            {
                did = await IdentityResolution.ResolveHandleAsync(
                    handle: handle,
                    httpClient: httpClient,
                    loggerFactory: loggerFactory,
                    cancellationToken: token).ConfigureAwait(false);
                if (did is null || !await IdentityResolution.VerifyHandleAsync(
                    handle: handle,
                    did: did,
                    httpClient: httpClient,
                    loggerFactory: loggerFactory,
                    cancellationToken: token).ConfigureAwait(false))
                {
                    return Failure("The handle and DID could not be verified in both directions.");
                }
            }

            if (did is null)
            {
                return Failure("The account identifier could not be resolved.");
            }

            DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
                did: did,
                httpClient: httpClient,
                loggerFactory: loggerFactory,
                cancellationToken: token).ConfigureAwait(false);
            if (document is null || document.Id != did)
            {
                return Failure("A matching DID document could not be resolved.");
            }

            LabelTarget resolved = target.WithRepository(did);
            if (resolved.Kind == LabelTargetKind.Account)
            {
                return LabelServiceResult<LabelTarget>.Success(resolved);
            }

            Uri? pds = await IdentityResolution.ResolvePdsAsync(
                did: did,
                httpClient: httpClient,
                loggerFactory: loggerFactory,
                cancellationToken: token).ConfigureAwait(false);
            if (pds is null)
            {
                return Failure("The DID document has no usable personal data server.");
            }

            AtUri uri = resolved.RecordUri!;
            AtProtoHttpResult<AtProtoRepositoryRecord> response = await AtProtoServer.GetRecord(
                repo: did,
                collection: uri.Collection!,
                rKey: uri.RecordKey!,
                cid: target.Cid,
                service: pds,
                accessCredentials: null,
                httpClient: httpClient,
                loggerFactory: loggerFactory,
                cancellationToken: token).ConfigureAwait(false);
            if (!response.Succeeded)
            {
                // Only an authoritative record/version absence is NotFound. Other HTTP errors retain
                // their original diagnostics rather than masquerading as a nonexistent target.
                bool missing = response.StatusCode == HttpStatusCode.NotFound ||
                    response.AtErrorDetail?.Error is "RecordNotFound";
                return LabelServiceResult<LabelTarget>.Failure(
                    new(
                        code: missing ? LabelServiceErrorCode.TargetNotFound : LabelServiceErrorCode.TargetResolutionFailed,
                        message: missing ? "The requested record version was not found." : "The target record could not be retrieved.",
                        statusCode: response.StatusCode,
                        atErrorDetail: response.AtErrorDetail));
            }

            if (response.Result.Uri != uri)
            {
                return Failure("The server returned a different record identity.");
            }

            if (target.Cid is not null && target.Cid != response.Result.Cid)
            {
                return LabelServiceResult<LabelTarget>.Failure(
                    new(
                        code: LabelServiceErrorCode.CidMismatch,
                        message: "The returned record CID differs from the requested version."));
            }

            return LabelServiceResult<LabelTarget>.Success(resolved);
        }
        catch (HttpRequestException exception)
        {
            return LabelServiceResult<LabelTarget>.Failure(
                new(
                    code: LabelServiceErrorCode.TargetResolutionFailed,
                    message: "The target lookup failed; inspect the HTTP diagnostic and retry the same request.",
                    statusCode: exception.StatusCode,
                    exception: exception));
        }
        catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
        {
            return LabelServiceResult<LabelTarget>.Failure(
                new(
                    code: LabelServiceErrorCode.TargetResolutionFailed,
                    message: "The target lookup timed out; retry the same request when the service is available.",
                    exception: exception));
        }
    }

    /// <summary>Reports unverifiable identity or location without misclassifying it as authoritative absence.</summary>
    private static LabelServiceResult<LabelTarget> Failure(string message) =>
        LabelServiceResult<LabelTarget>.Failure(
            new(
                code: LabelServiceErrorCode.TargetResolutionFailed,
                message: message));
}
