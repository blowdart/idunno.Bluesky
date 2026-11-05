// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Observes the latest record only after independently resolving its repository's PDS.</summary>
internal static class ReportRecordResolver
{
    /// <summary>Returns a recoverable failure observation without inventing evidence for deleted or unavailable records.</summary>
    internal static async Task<LabelRecordResolution> ResolveAsync(
        AtUri uri,
        HttpClient client,
        TimeProvider clock,
        CancellationToken token)
    {
        Uri? pds = null;
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5), clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        try
        {
            Did did = (Did)uri.Repo;
            pds = await IdentityResolution.ResolvePdsAsync(did: did, httpClient: client, cancellationToken: linked.Token).ConfigureAwait(false);
            if (pds is null)
            {
                return new(null, "pds-unavailable", null, clock.GetUtcNow());
            }

            AtProtoHttpResult<AtProtoRepositoryRecord> result = await AtProtoServer.GetRecord(
                repo: did,
                collection: uri.Collection!,
                rKey: uri.RecordKey!,
                cid: null,
                service: pds,
                accessCredentials: null,
                httpClient: client,
                cancellationToken: linked.Token).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return new(
                    null,
                    result.AtErrorDetail?.Error == "RecordNotFound" ? "record-not-found" : "record-unavailable",
                    pds.AbsoluteUri,
                    clock.GetUtcNow());
            }

            return result.Result.Uri == uri
                ? new(result.Result.Cid.ToString(), "resolved", pds.AbsoluteUri, clock.GetUtcNow())
                : new(null, "inconsistent-record", pds.AbsoluteUri, clock.GetUtcNow());
        }
        catch (HttpRequestException)
        {
            return new(null, "pds-unavailable", pds?.AbsoluteUri, clock.GetUtcNow());
        }
        catch (JsonException)
        {
            return new(null, "invalid-response", pds?.AbsoluteUri, clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return new(null, "pds-unavailable", pds?.AbsoluteUri, clock.GetUtcNow());
        }
    }
}
