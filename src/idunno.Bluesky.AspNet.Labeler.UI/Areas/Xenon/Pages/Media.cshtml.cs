// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using idunno.AtProto;
using idunno.AtProto.Sync;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages;

/// <summary>Streams an expiring record-referenced video only after live team authorization.</summary>
public sealed partial class MediaModel : PageModel
{
    /// <summary>Reads a protected blob reference without accepting arbitrary media URLs from the browser.</summary>
    /// <param name="token">The short-lived reference generated while reading a repository record.</param>
    /// <returns>The bounded video response or an explicit failure status.</returns>
    public async Task<IActionResult> OnGetAsync(string? token)
    {
        ILogger<MediaModel> logger = HttpContext.RequestServices.GetRequiredService<ILogger<MediaModel>>();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Did? did;
        Cid? cid;
        try
        {
            string[] reference = HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(SubjectPreviewService.VideoPurpose).ToTimeLimitedDataProtector()
                .Unprotect(token ?? string.Empty).Split('\n');
            if (reference.Length != 2 || !Did.TryParse(reference[0], out did) || !Cid.TryParse(reference[1], out cid))
            {
                MediaFailure(logger, "The video reference is invalid.", null);
                return BadRequest("Invalid video reference. Reload the details page.");
            }
        }
        catch (CryptographicException exception)
        {
            MediaFailure(logger, "The video reference is invalid or expired.", exception);
            return BadRequest("Invalid or expired video reference. Reload the details page.");
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        using HttpClient client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(SubjectPreviewService.ClientName);
        client.Timeout = TimeSpan.FromMinutes(2);
        try
        {
            Uri? pds = await SubjectPreviewService.ResolvePersonalDataServerAsync(client, did, deadline.Token).ConfigureAwait(false);
            if (pds is null)
            {
                MediaFailure(logger, "The author's HTTPS PDS is unavailable.", null);
                return StatusCode(StatusCodes.Status502BadGateway, "Video server unavailable.");
            }

            AtProtoHttpResult<BlobContent> result = await AtProtoServer.GetBlob(
                did: did,
                cid: cid,
                service: pds,
                httpClient: client,
                cancellationToken: deadline.Token).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                MediaFailure(logger, "The PDS did not return the video blob.", null);
                return StatusCode(StatusCodes.Status502BadGateway, "Video unavailable.");
            }

            using BlobContent blob = result.Result;
            byte[] buffer = new byte[65_536];
            await blob.Content.ReadExactlyAsync(buffer.AsMemory(0, 12), deadline.Token).ConfigureAwait(false);
            if (blob.ContentType?.MediaType != "video/mp4" || !buffer.AsSpan(4, 4).SequenceEqual("ftyp"u8))
            {
                MediaFailure(logger, "The video is not a supported MP4 file.", null);
                return StatusCode(StatusCodes.Status502BadGateway, "Unsupported video format.");
            }

            Response.ContentType = "video/mp4";
            await Response.Body.WriteAsync(buffer.AsMemory(0, 12), deadline.Token).ConfigureAwait(false);
            long total = 12;
            int count;
            while ((count = await blob.Content.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
            {
                total += count;
                if (total > 104_857_600)
                {
                    MediaFailure(logger, "The video exceeds the 100 MiB preview limit.", null);
                    HttpContext.Abort();
                    return new EmptyResult();
                }

                await Response.Body.WriteAsync(buffer.AsMemory(0, count), deadline.Token).ConfigureAwait(false);
            }

            return new EmptyResult();
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException or System.Text.Json.JsonException)
        {
            MediaFailure(logger, "The video transfer failed or exceeded its time or size limit.", exception);
            if (Response.HasStarted)
            {
                HttpContext.Abort();
                return new EmptyResult();
            }

            return StatusCode(StatusCodes.Status502BadGateway, "Video transfer failed.");
        }
    }

    /// <summary>Records media failures without logging protected references or confidential subject identifiers.</summary>
    [LoggerMessage(1, LogLevel.Warning, "Xenon video unavailable: {Reason}")]
    private static partial void MediaFailure(ILogger logger, string reason, Exception? exception);
}
