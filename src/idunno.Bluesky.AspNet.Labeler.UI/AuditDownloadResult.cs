// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Streams a fixed audit prefix in bounded pages, failing the response rather than completing a partial or revoked export.</summary>
/// <param name="query">The validated filters to apply independently of visible table paging and sorting.</param>
internal sealed class AuditDownloadResult(LabelTableQuery query) : IActionResult
{
    /// <summary>Negotiates compression, records the export start and writes a source-generated JSON array under repeated live authorization.</summary>
    /// <param name="context">The authorized browser action context.</param>
    /// <returns>A task representing the response, which is aborted on an incomplete export.</returns>
    public async Task ExecuteResultAsync(ActionContext context)
    {
        HttpContext http = context.HttpContext;
        CancellationToken token = http.RequestAborted;
        HttpResponse response = http.Response;
        response.Headers.CacheControl = "no-store";
        response.Headers.AppendCommaSeparatedValues(HeaderNames.Vary, HeaderNames.AcceptEncoding);
        response.Headers.XContentTypeOptions = "nosniff";
        string[] encodings = http.Request.Headers.GetCommaSeparatedValues(HeaderNames.AcceptEncoding);
        IList<StringWithQualityHeaderValue>? accepted = [];
        if (encodings.Length > 0 && !StringWithQualityHeaderValue.TryParseStrictList(encodings, out accepted))
        {
            response.StatusCode = StatusCodes.Status400BadRequest;
            await response.WriteAsync("Invalid Accept-Encoding header.", token).ConfigureAwait(false);
            return;
        }

        string? encoding = SelectEncoding(accepted ?? []);
        if (encoding is null)
        {
            response.StatusCode = StatusCodes.Status406NotAcceptable;
            await response.WriteAsync("No supported response encoding is acceptable.", token).ConfigureAwait(false);
            return;
        }

        LabelerUiAdministration administration = http.RequestServices.GetRequiredService<LabelerUiAdministration>();
        AdministrationResponse started = await administration.BeginAuditExportAsync(http.User, query, token).ConfigureAwait(false);
        if (!started.Succeeded)
        {
            response.StatusCode = started.Error == "Unauthorized" ? StatusCodes.Status403Forbidden : StatusCodes.Status503ServiceUnavailable;
            response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(
                utf8Json: response.Body,
                value: started,
                jsonTypeInfo: AdministrationJsonContext.Default.AdministrationResponse,
                cancellationToken: token).ConfigureAwait(false);
            return;
        }

        LabelTableQuery export = query with
        {
            Page = 1,
            Sort = LabelTableSort.Number,
            Ascending = true,
            AuditAfterSequence = 0,
            AuditThroughSequence = started.Sequence!.Value
        };
        response.ContentType = "application/json; charset=utf-8";
        response.Headers.ContentDisposition = "attachment; filename=\"xenon-audit.json\"";
        if (encoding != "identity")
        {
            response.Headers.ContentEncoding = encoding;
        }

        bool complete = false;
        try
        {
            Stream? compressed = encoding switch
            {
                "br" => new BrotliStream(response.Body, CompressionLevel.Fastest, leaveOpen: true),
                "gzip" => new GZipStream(response.Body, CompressionLevel.Fastest, leaveOpen: true),
                _ => null
            };
            await using (ConfiguredAsyncDisposable disposal = (compressed ?? Stream.Null).ConfigureAwait(false))
            {
                Stream output = compressed ?? response.Body;
                await output.WriteAsync("["u8.ToArray(), token).ConfigureAwait(false);
                bool first = true;
                while (true)
                {
                    AdministrationResponse page = await administration.ReadAuditExportPageAsync(http.User, export, token).ConfigureAwait(false);
                    if (!page.Succeeded)
                    {
                        throw new InvalidOperationException("The audit download stopped because live authorization or storage access failed.");
                    }

                    AdministrationAuditView[] entries = page.Audit!;
                    foreach (AdministrationAuditView entry in entries)
                    {
                        if (!first)
                        {
                            await output.WriteAsync(","u8.ToArray(), token).ConfigureAwait(false);
                        }

                        await JsonSerializer.SerializeAsync(
                            utf8Json: output,
                            value: entry,
                            jsonTypeInfo: AdministrationJsonContext.Default.AdministrationAuditView,
                            cancellationToken: token).ConfigureAwait(false);
                        first = false;
                        export = export with { AuditAfterSequence = entry.Sequence };
                    }

                    if (entries.Length < LabelTableQuery.PageSize)
                    {
                        break;
                    }

                    await output.FlushAsync(token).ConfigureAwait(false);
                }

                await output.WriteAsync("]"u8.ToArray(), token).ConfigureAwait(false);
            }

            complete = true;
        }
        finally
        {
            if (!complete)
            {
                http.Abort();
            }
        }
    }

    /// <summary>Honors explicit quality values and wildcard exclusions, preferring Brotli then gzip on ties.</summary>
    private static string? SelectEncoding(IList<StringWithQualityHeaderValue> accepted)
    {
        double wildcard = Quality(accepted, "*") ?? 0;
        double identity = Quality(accepted, "identity") ?? (Quality(accepted, "*") == 0 ? 0 : 1);
        double brotli = Quality(accepted, "br") ?? wildcard;
        double gzip = Quality(accepted, "gzip") ?? wildcard;
        if (brotli > 0 && brotli >= gzip && brotli >= identity)
        {
            return "br";
        }

        if (gzip > 0 && gzip >= identity)
        {
            return "gzip";
        }

        return identity > 0 ? "identity" : null;
    }

    /// <summary>Reads the greatest explicitly advertised quality for one case-insensitive content coding.</summary>
    private static double? Quality(IList<StringWithQualityHeaderValue> accepted, string coding)
    {
        double? quality = null;
        foreach (StringWithQualityHeaderValue item in accepted.Where(item => string.Equals(item.Value.Value, coding, StringComparison.OrdinalIgnoreCase)))
        {
            quality = Math.Max(quality ?? 0, item.Quality ?? 1);
        }

        return quality;
    }
}
