// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Reads anonymous repository content through the hardened client, independently of moderator block relationships.</summary>
internal sealed partial class SubjectPreviewService(
    IHttpClientFactory clients,
    ILoggerFactory loggerFactory,
    IDataProtectionProvider protection,
    TimeProvider clock)
{
    /// <summary>Gets the named client used for bounded public AppView and allowlisted CDN requests.</summary>
    internal const string ClientName = "Xenon.PublicPreviews";

    /// <summary>Separates expiring media references from other protected application data.</summary>
    internal const string VideoPurpose = "Xenon.PreviewVideo.v1";

    /// <summary>Gets the fixed public read-only AppView origin; report data never selects the service.</summary>
    private static readonly Uri s_appView = new("https://public.api.bsky.app");

    /// <summary>Loads current public content after the caller has been authorized to inspect the report or case.</summary>
    internal async Task<SubjectPreview> LoadAsync(JsonElement subject, CancellationToken cancellationToken)
    {
        string? target = subject.TryGetProperty("uri", out JsonElement uriValue) ? uriValue.GetString() : null;
        AtUri? uri = null;
        Did? did;
        bool post = false;
        string kind = "Profile";
        if (target is not null)
        {
            if (!AtUri.TryParse(target, out uri) || uri.Repo is not Did recordDid)
            {
                return new("Record", null, Error: "This record does not have a supported public preview.");
            }

            did = recordDid;
            post = uri.Collection?.ToString() == "app.bsky.feed.post" && uri.RecordKey is not null;
            kind = uri.Collection?.ToString() switch
            {
                "app.bsky.feed.post" when post => "Post",
                "app.bsky.actor.profile" when uri.RecordKey?.ToString() == "self" => "Profile",
                "app.bsky.graph.list" when uri.RecordKey is not null => "List",
                "app.bsky.graph.starterpack" when uri.RecordKey is not null => "Starter pack",
                _ => "Record"
            };
            if (kind == "Record")
            {
                return new("Record", null, Error: "Public previews are available only for Bluesky posts, profiles, lists and starter packs.");
            }
        }
        else if (!Did.TryParse(subject.GetProperty("did").GetString()!, out did))
        {
            return new("Profile", null, Error: "The report's profile identifier is invalid.");
        }

        string url = $"https://bsky.app/profile/{did.Value}";
        if (kind is "Post" or "List")
        {
            url += $"/{(post ? "post" : "lists")}/{Uri.EscapeDataString(uri!.RecordKey!.ToString())}";
        }
        else if (kind == "Starter pack")
        {
            url = $"https://bsky.app/starter-pack/{did.Value}/{Uri.EscapeDataString(uri!.RecordKey!.ToString())}";
        }

        using HttpClient client = clients.CreateClient(ClientName);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            Uri? pds = await ResolvePersonalDataServerAsync(client, did, deadline.Token).ConfigureAwait(false);
            if (pds is null)
            {
                return Unavailable(kind, url, "The author's personal data server could not be resolved.");
            }

            AtUri recordUri = uri ?? new($"at://{did}/app.bsky.actor.profile/self");
            AtProtoHttpResult<AtProtoRepositoryRecord> record = await AtProtoServer.GetRecord(
                repo: did,
                collection: recordUri.Collection!,
                rKey: recordUri.RecordKey!,
                cid: null,
                service: pds,
                accessCredentials: null,
                httpClient: client,
                loggerFactory: loggerFactory,
                maximumResponseSize: 524_288,
                cancellationToken: deadline.Token).ConfigureAwait(false);
            if (!record.Succeeded || record.Result.Uri != recordUri || record.Result.Value is not { } value ||
                Text(value, "$type") != recordUri.Collection!.ToString() || post && Text(value, "text") is null ||
                kind is "List" or "Starter pack" && Text(value, "name") is null)
            {
                return Unavailable(kind, url, "The record is unavailable or the PDS returned a different record identity or type.");
            }

            JsonObject? profileRecord = kind switch
            {
                "Post" => await ReadAuthorProfileAsync(client: client, did: did, pds: pds, token: deadline.Token).ConfigureAwait(false),
                "Profile" => value,
                _ => null
            };
            List<PreviewImage> photos = [];
            PreviewImage? avatar = null;
            PreviewImage? banner = null;
            if (kind == "Profile")
            {
                foreach (string field in new[] { "avatar", "banner" })
                {
                    if (profileRecord?[field] is JsonObject blob)
                    {
                        PreviewImage image = await LoadBlobImageAsync(
                            client: client,
                            did: did,
                            pds: pds,
                            blob: blob,
                            alt: "Profile " + field,
                            token: deadline.Token).ConfigureAwait(false);
                        if (field == "avatar")
                        {
                            avatar = image;
                        }
                        else
                        {
                            banner = image;
                        }
                    }
                }
            }

            JsonObject? embed = value["embed"] as JsonObject;
            JsonObject? media = Text(embed, "$type") == "app.bsky.embed.recordWithMedia" ? embed?["media"] as JsonObject : embed;
            if (post && Text(media, "$type") == "app.bsky.embed.images" && media?["images"] is JsonArray images)
            {
                foreach (JsonObject image in images.OfType<JsonObject>().Take(4))
                {
                    photos.Add(await LoadBlobImageAsync(
                        client: client,
                        did: did,
                        pds: pds,
                        blob: image["image"] as JsonObject,
                        alt: Text(image, "alt") ?? "",
                        token: deadline.Token).ConfigureAwait(false));
                }
            }
            if (post && Text(media, "$type") == "app.bsky.embed.gallery" && media?["items"] is JsonArray items)
            {
                foreach (JsonObject image in items.OfType<JsonObject>().Where(item => Text(item, "$type") == "app.bsky.embed.gallery#image").Take(20))
                {
                    photos.Add(await LoadBlobImageAsync(
                        client: client,
                        did: did,
                        pds: pds,
                        blob: image["image"] as JsonObject,
                        alt: Text(image, "alt") ?? "",
                        token: deadline.Token).ConfigureAwait(false));
                }
            }

            string? videoToken = null;
            int? videoWidth = null;
            int? videoHeight = null;
            if (post && Text(media, "$type") == "app.bsky.embed.video" &&
                media?["video"] is JsonObject video && Text(video, "mimeType") == "video/mp4" &&
                BlobCid(video) is { } videoCid)
            {
                videoToken = protection.CreateProtector(VideoPurpose).ToTimeLimitedDataProtector()
                    .Protect($"{did.Value}\n{videoCid}", TimeSpan.FromMinutes(10));
                if (media["aspectRatio"] is JsonObject ratio &&
                    ratio["width"] is JsonValue widthValue && widthValue.TryGetValue(out int width) &&
                    ratio["height"] is JsonValue heightValue && heightValue.TryGetValue(out int height) &&
                    width is > 0 and <= 1_000_000 && height is > 0 and <= 1_000_000)
                {
                    videoWidth = width;
                    videoHeight = height;
                }
            }

            // AppView metadata is supplementary: it must never gate repository content.
            ProfileViewDetailed? identity = await ReadIdentityAsync(client, did, cancellationToken).ConfigureAwait(false);
            (string[] warnings, bool moderationUnavailable) = await ReadMediaWarningsAsync(
                client: client,
                did: did,
                recordUri: recordUri,
                recordCid: record.Result.Cid,
                record: value,
                token: cancellationToken).ConfigureAwait(false);
            string? reportedCid = subject.TryGetProperty("cid", out JsonElement cid) ? cid.GetString() : null;
            return new(
                Kind: kind,
                Url: url,
                DisplayName: kind is "List" or "Starter pack" ? Text(value, "name") : Text(profileRecord, "displayName"),
                Handle: identity?.Handle.Value ?? did.Value,
                Text: Text(value, post ? "text" : "description"),
                CreatedAt: post && DateTimeOffset.TryParse(Text(value, "createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset created) ? created : null,
                Cid: record.Result.Cid.ToString(),
                DifferentVersion: reportedCid is not null && reportedCid != record.Result.Cid.ToString(),
                Images: [.. photos],
                MediaNotice: embed is not null && Text(embed, "$type") is not ("app.bsky.embed.images" or "app.bsky.embed.gallery") && videoToken is null
                    ? "Additional media, quoted posts or external cards are not expanded here. Open the post to inspect its complete content."
                    : null,
                Verified: identity?.Verification?.VerifiedStatus == VerificationStatus.Valid,
                VideoToken: videoToken,
                VideoAlt: Text(media, "alt"),
                Gallery: Text(media, "$type") == "app.bsky.embed.gallery",
                Avatar: avatar,
                Banner: banner,
                Followers: identity?.FollowersCount,
                Following: identity?.FollowsCount,
                Posts: identity?.PostsCount,
                MediaWarnings: warnings,
                ModerationUnavailable: moderationUnavailable,
                VideoWidth: videoWidth,
                VideoHeight: videoHeight,
                MatchesReportedVersion: reportedCid is not null && reportedCid == record.Result.Cid.ToString());
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(kind, url, "The public preview request timed out. Reload to try again.", exception);
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(kind, url, "The public preview service could not be reached. Reload to try again.", exception);
        }
        catch (JsonException exception)
        {
            return Unavailable(kind, url, "The public preview service returned an invalid response.", exception);
        }
    }

    /// <summary>Accepts an HTTPS PDS only from a matching DID document; the HTTP handler fences resolved destination addresses.</summary>
    internal static async Task<Uri?> ResolvePersonalDataServerAsync(HttpClient client, Did did, CancellationToken token)
    {
        DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
            did: did,
            httpClient: client,
            maximumResponseSize: 524_288,
            cancellationToken: token).ConfigureAwait(false);
        if (document is null || document.Id != did)
        {
            return null;
        }

        Uri? pds = document.Services.FirstOrDefault(service => service.Id == "#atproto_pds")?.ServiceEndpoint;
        return pds is { IsAbsoluteUri: true, Scheme: "https", UserInfo.Length: 0, Query.Length: 0, Fragment.Length: 0 } ? pds : null;
    }

    /// <summary>Reads optional author text without making post availability depend on a profile record.</summary>
    private async Task<JsonObject?> ReadAuthorProfileAsync(
        HttpClient client,
        Did did,
        Uri pds,
        CancellationToken token)
    {
        try
        {
            AtProtoHttpResult<AtProtoRepositoryRecord> record = await AtProtoServer.GetRecord(
                repo: did,
                collection: new("app.bsky.actor.profile"),
                rKey: new("self"),
                cid: null,
                service: pds,
                accessCredentials: null,
                httpClient: client,
                loggerFactory: loggerFactory,
                maximumResponseSize: 524_288,
                cancellationToken: token).ConfigureAwait(false);
            if (record.Succeeded && record.Result.Uri == new AtUri($"at://{did}/app.bsky.actor.profile/self") &&
                Text(record.Result.Value, "$type") == "app.bsky.actor.profile")
            {
                return record.Result.Value;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Author", "The optional author profile record could not be loaded.", exception);
        }

        PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Author", "The optional author profile record is unavailable.", null);
        return null;
    }

    /// <summary>Fetches anonymous verification metadata separately, retaining the PDS preview when AppView refuses or omits it.</summary>
    private async Task<ProfileViewDetailed?> ReadIdentityAsync(HttpClient client, Did did, CancellationToken token)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            AtProtoHttpResult<ProfileViewDetailed> response = await BlueskyServer.GetProfile(
                actor: did,
                service: s_appView,
                accessCredentials: null,
                httpClient: client,
                loggerFactory: loggerFactory,
                maximumResponseSize: 524_288,
                cancellationToken: deadline.Token).ConfigureAwait(false);
            if (response.Succeeded && response.Result.Did == did)
            {
                return response.Result;
            }
        }
        catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
        {
            PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Identity", "Optional AppView metadata timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Identity", "Optional AppView metadata could not be reached.", exception);
        }
        catch (JsonException exception)
        {
            PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Identity", "Optional AppView metadata was invalid.", exception);
        }

        PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Identity", "Handle and verification metadata are unavailable; displaying the repository DID.", null);
        return null;
    }

    /// <summary>Reads only string-shaped remote fields; other shapes are not interpreted as text.</summary>
    private static string? Text(JsonObject? value, string name) =>
        value?[name] is JsonValue field && field.TryGetValue(out string? text) ? text : null;

    /// <summary>Accepts only canonical blob references declared by a repository record.</summary>
    private static Cid? BlobCid(JsonObject blob) =>
        Text(blob["ref"] as JsonObject, "$link") is { } value && Cid.TryParse(value, out Cid? cid) ? cid : null;

    /// <summary>Fetches a record-referenced raster blob from the author's PDS, never a record-selected media URL.</summary>
    private async Task<PreviewImage> LoadBlobImageAsync(
        HttpClient client,
        Did did,
        Uri pds,
        JsonObject? blob,
        string alt,
        CancellationToken token)
    {
        if (blob is null || BlobCid(blob) is not { } cid || Text(blob, "mimeType") is not ("image/jpeg" or "image/png" or "image/webp" or "image/gif"))
        {
            PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Image", "The record contains an unsupported image reference.", null);
            return new(null, alt, "Image unavailable: the record contains an unsupported image reference.");
        }

        Uri url = new(pds, $"/xrpc/com.atproto.sync.getBlob?did={Uri.EscapeDataString(did.Value)}&cid={Uri.EscapeDataString(cid.ToString())}");
        return await LoadImageAsync(client: client, uri: url, alt: alt, cancellationToken: token).ConfigureAwait(false);
    }

    /// <summary>Reads at most ten MiB of raster data through the SSRF-protected client, never trusting a remote content length.</summary>
    private async Task<PreviewImage> LoadImageAsync(
        HttpClient client,
        Uri uri,
        string alt,
        CancellationToken cancellationToken)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0)
        {
            PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Image", "The PDS image URL is not HTTPS.", null);
            return new(null, alt, "Image unavailable: an HTTPS personal data server is required.");
        }

        try
        {
            using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode || mediaType is not ("image/jpeg" or "image/png" or "image/webp" or "image/gif"))
            {
                PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Image", "The PDS did not return a supported raster image.", null);
                return new(null, alt, "Image unavailable: the PDS did not return a supported image.");
            }

            using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using MemoryStream bytes = new();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
            {
                if (bytes.Length + count > 10_485_760)
                {
                    PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Image", "The preview image exceeds the size limit.", null);
                    return new(null, alt, "Image unavailable: the preview exceeds the ten MiB limit.");
                }

                await bytes.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            }

            return new($"data:{mediaType};base64,{Convert.ToBase64String(bytes.ToArray())}", alt);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
        {
            PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), "Image", "The PDS image transfer failed or timed out.", exception);
            return new(null, alt, "Image unavailable: the PDS transfer failed or timed out.");
        }
    }

    /// <summary>Records a classified preview failure without exposing server responses or treating it as a moderation failure.</summary>
    private SubjectPreview Unavailable(string kind, string url, string reason, Exception? exception = null)
    {
        PreviewUnavailable(loggerFactory.CreateLogger<SubjectPreviewService>(), kind, reason, exception);
        return new(kind, url, Error: reason);
    }

    /// <summary>Records an unavailable public preview while retaining the authorized moderation page.</summary>
    [LoggerMessage(1, LogLevel.Warning, "Xenon {Kind} preview unavailable: {Reason}")]
    private static partial void PreviewUnavailable(ILogger logger, string kind, string reason, Exception? exception);
}
