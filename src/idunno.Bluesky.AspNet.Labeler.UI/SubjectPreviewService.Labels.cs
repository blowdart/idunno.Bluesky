// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler.UI;

internal sealed partial class SubjectPreviewService
{
    /// <summary>Gets the fixed public Bluesky moderation endpoint, independent of the reported subject.</summary>
    private static readonly Uri s_moderation = new("https://mod.bsky.app");

    /// <summary>Combines current-record self-labels with bounded, anonymous reads of Bluesky's account and record labels.</summary>
    private async Task<(string[] Warnings, bool Unavailable)> ReadMediaWarningsAsync(
        HttpClient client,
        Did did,
        AtUri recordUri,
        Cid recordCid,
        JsonObject record,
        CancellationToken token)
    {
        HashSet<string> warnings = new(StringComparer.Ordinal);
        if (record["labels"] is JsonObject selfLabels &&
            Text(selfLabels, "$type") == "com.atproto.label.defs#selfLabels" &&
            selfLabels["values"] is JsonArray values)
        {
            foreach (JsonObject value in values.OfType<JsonObject>())
            {
                if (MediaWarning(Text(value, "val")) is { } warning)
                {
                    warnings.Add(warning + " (self-label)");
                }
            }
        }

        List<Label> labels = [];
        bool unavailable = false;
        Exception? failure = null;
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            string? cursor = null;
            HashSet<string> cursors = new(StringComparer.Ordinal);
            for (int page = 0; page < 4; page++)
            {
                AtProtoHttpResult<PagedReadOnlyCollection<Label>> response;
                try
                {
                    response = await AtProtoServer.QueryLabels(
                        uriPatterns: [did.Value, recordUri.ToString()],
                        sources: [WellKnownDistributedIdentifiers.BlueskyModerationLabeler],
                        limit: 250,
                        cursor: cursor,
                        service: s_moderation,
                        accessCredentials: null,
                        httpClient: client,
                        loggerFactory: loggerFactory,
                        maximumResponseSize: 524_288,
                        cancellationToken: deadline.Token).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException ||
                    exception is OperationCanceledException && !token.IsCancellationRequested)
                {
                    unavailable = true;
                    failure = exception;
                    break;
                }
                if (!response.Succeeded)
                {
                    unavailable = true;
                    break;
                }

                labels.AddRange(response.Result.Where(label =>
                    label.Source == WellKnownDistributedIdentifiers.BlueskyModerationLabeler &&
                    (label.Uri == recordUri.ToString() && (label.Cid is null || label.Cid == recordCid) ||
                     label.Uri == did.Value && label.Cid is null)));
                cursor = response.Result.Cursor;
                if (string.IsNullOrEmpty(cursor))
                {
                    break;
                }
                if (!cursors.Add(cursor) || page == 3)
                {
                    unavailable = true;
                    break;
                }
            }

            DateTimeOffset now = clock.GetUtcNow();
            Label[] active = labels
                .GroupBy(label => (label.Uri, label.Cid, label.Value))
                .Select(group => group.OrderByDescending(label => label.CreationTimestamp).ThenByDescending(label => label.IsNegationLabel).First())
                .Where(label => !label.IsNegationLabel && (label.ExpiresAt is null || label.ExpiresAt > now))
                .ToArray();
            foreach (Label label in active)
            {
                if (MediaWarning(label.Value) is { } warning)
                {
                    warnings.Add(warning + " (Bluesky moderation)");
                }
            }

            // Custom adult/media definitions extend the built-in warnings, but ordinary spam/identity labels do not blur images.
            if (active.Any(label => MediaWarning(label.Value) is null))
            {
                AtProtoHttpResult<ICollection<LabelerView>> definitions = await BlueskyServer.GetLabelerServices(
                    dids: [WellKnownDistributedIdentifiers.BlueskyModerationLabeler],
                    getDetailedViews: true,
                    service: s_appView,
                    accessCredentials: null,
                    httpClient: client,
                    loggerFactory: loggerFactory,
                    maximumResponseSize: 524_288,
                    cancellationToken: deadline.Token).ConfigureAwait(false);
                LabelerViewDetailed? service = definitions.Succeeded
                    ? definitions.Result.OfType<LabelerViewDetailed>().FirstOrDefault(view =>
                        view.Creator.Did == WellKnownDistributedIdentifiers.BlueskyModerationLabeler &&
                        view.Uri == new AtUri($"at://{WellKnownDistributedIdentifiers.BlueskyModerationLabeler}/app.bsky.labeler.service/self"))
                    : null;
                if (service is null)
                {
                    unavailable = true;
                }
                else
                {
                    foreach (LabelValueDefinition definition in service.Policies.LabelValueDefinitions)
                    {
                        if ((definition.AdultOnly || definition.Blurs == "media") &&
                            active.Any(label => label.Value == definition.Identifier && MediaWarning(label.Value) is null))
                        {
                            warnings.Add(definition.Identifier + " (Bluesky moderation)");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
        {
            unavailable = true;
            failure = exception;
        }
        catch (HttpRequestException exception)
        {
            unavailable = true;
            failure = exception;
        }
        catch (System.Text.Json.JsonException exception)
        {
            unavailable = true;
            failure = exception;
        }

        if (unavailable)
        {
            PreviewUnavailable(
                logger: loggerFactory.CreateLogger<SubjectPreviewService>(),
                kind: "Moderation",
                reason: "Bluesky media-warning labels could not be fully checked; known warnings still apply.",
                exception: failure);
        }

        return (warnings.Order(StringComparer.Ordinal).ToArray(), unavailable);
    }

    /// <summary>Names built-in media warnings without interpreting unrelated labels as sensitive imagery.</summary>
    private static string? MediaWarning(string? value) => value switch
    {
        SelfLabelValues.Porn => "Adult content",
        SelfLabelValues.Sexual => "Sexual content",
        SelfLabelValues.Nudity => "Nudity",
        SelfLabelValues.GraphicMedia => "Graphic or violent content",
        "!hide" => "Content hidden by its label",
        "!warn" => "Content warning",
        _ => null
    };
}
