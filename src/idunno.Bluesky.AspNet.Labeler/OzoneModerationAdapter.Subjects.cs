// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class OzoneModerationAdapter
{
    /// <summary>Extracts a repository identity without treating an unversioned record as an account.</summary>
    private static string EventRepository(string subject) =>
        subject.StartsWith("at://", StringComparison.Ordinal) ? new AtUri(subject).Repo.ToString() : subject;

    /// <summary>Hydrates bounded event subjects with the same resolution and authority as individual reads.</summary>
    private async Task<byte[]> GetSubjectsAsync(
        HttpContext context,
        LabelAdministrationCaller caller,
        ILabelModerationStore cases,
        bool records,
        CancellationToken token)
    {
        string parameter = records ? "uris" : "dids";
        ValidateQuery(context, [parameter]);
        string[] subjects = [.. context.Request.Query[parameter].Select(value =>
            value ?? throw new JsonException("A subject must not be null."))];
        if (subjects.Length is < 1 or > 100)
        {
            throw new JsonException("Supply between one and one hundred subjects.");
        }
        foreach (string subject in subjects)
        {
            if (records)
            {
                if (!AtUri.TryParse(subject, out AtUri? uri) ||
                    uri.Repo is not Did || uri.Collection is null || uri.RecordKey is null)
                {
                    throw new JsonException("A complete record URI with a DID repository is required.");
                }
            }
            else
            {
                _ = new Did(subject);
            }
        }
        List<byte[]> views = [];
        foreach (string subject in subjects.Distinct(StringComparer.Ordinal))
        {
            try
            {
                views.Add(records
                    ? await RecordAsync(
                        caller: caller,
                        uri: new(subject),
                        cid: null,
                        cases: cases,
                        token: token).ConfigureAwait(false)
                    : await RepoAsync(
                        caller: caller,
                        did: new(subject),
                        cases: cases,
                        token: token).ConfigureAwait(false));
            }
            catch (OzoneOperationException exception) when (records && exception.Code == "RecordNotFound")
            {
                views.Add(Encode(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("$type", "tools.ozone.moderation.defs#recordViewNotFound");
                    writer.WriteString("uri", subject);
                    writer.WriteEndObject();
                }));
            }
        }
        return Encode(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray(records ? "records" : "repos");
            foreach (byte[] view in views)
            {
                using JsonDocument document = JsonDocument.Parse(view);
                writer.WriteStartObject();
                if (!document.RootElement.TryGetProperty("$type", out _))
                {
                    writer.WriteString("$type", records
                        ? "tools.ozone.moderation.defs#recordViewDetail"
                        : "tools.ozone.moderation.defs#repoViewDetail");
                }
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    /// <summary>Enriches the stock Ozone preview through the public AppView without forwarding service credentials.</summary>
    private async Task<byte[]> GetProfileAsync(HttpContext context, CancellationToken token)
    {
        ValidateQuery(context, ["actor"]);
        AtIdentifier actor = AtIdentifier.Create(RequiredQuery(context, "actor"));
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor=" + Uri.EscapeDataString(actor.ToString()));
        using HttpResponseMessage response = await identity.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new OzoneOperationException(
                "ServiceUnavailable", "The public AppView could not retrieve the requested profile.");
        }
        using Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        byte[] payload;
        JsonDocument document;
        try
        {
            payload = await AdministrationWire.ReadAsync(stream, 1024 * 1024, token).ConfigureAwait(false);
            document = JsonDocument.Parse(payload);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            throw new OzoneOperationException("ServiceUnavailable", "The public AppView returned an invalid or oversized profile.");
        }
        using JsonDocument profileDocument = document;
        JsonElement profile = document.RootElement;
        if (profile.ValueKind != JsonValueKind.Object ||
            !profile.TryGetProperty("did", out JsonElement identifier) ||
            identifier.ValueKind != JsonValueKind.String ||
            !Did.TryParse(identifier.GetString()!, out Did? returnedDid) ||
            !profile.TryGetProperty("handle", out JsonElement handle) ||
            handle.ValueKind != JsonValueKind.String ||
            actor is Did did && returnedDid != did)
        {
            throw new OzoneOperationException("ServiceUnavailable", "The public AppView returned a different profile identity.");
        }
        return payload;
    }

    /// <summary>Retrieves a bounded public thread preview without forwarding moderator credentials or changing report evidence.</summary>
    private async Task<byte[]> GetPostThreadAsync(HttpContext context, CancellationToken token)
    {
        ValidateQuery(context, ["uri", "depth", "parentHeight"]);
        string uri = RequiredQuery(context, "uri");
        if (!AtUri.TryParse(uri, out AtUri? record) ||
            record.Collection?.ToString() != "app.bsky.feed.post" || record.RecordKey is null)
        {
            throw new JsonException("A complete post URI is required.");
        }
        StringBuilder query = new("uri=" + Uri.EscapeDataString(uri));
        foreach (string parameter in context.Request.Query.Keys.Where(parameter => parameter is "depth" or "parentHeight"))
        {
            if (!int.TryParse(Query(context, parameter), NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
                value is < 0 or > 1000)
            {
                throw new JsonException("Thread depth and parent height must be integers between 0 and 1000.");
            }
            query.Append('&').Append(parameter).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
        }
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "https://public.api.bsky.app/xrpc/app.bsky.feed.getPostThread?" + query);
        using HttpResponseMessage response = await identity.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new OzoneOperationException("ServiceUnavailable", "The public AppView could not retrieve the requested post.");
        }
        using Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        byte[] payload;
        JsonDocument document;
        try
        {
            payload = await AdministrationWire.ReadAsync(stream, 1024 * 1024, token).ConfigureAwait(false);
            document = JsonDocument.Parse(payload);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            throw new OzoneOperationException("ServiceUnavailable", "The public AppView returned an invalid or oversized thread.");
        }
        using JsonDocument threadDocument = document;
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("thread", out JsonElement thread) || thread.ValueKind != JsonValueKind.Object ||
            !thread.TryGetProperty("$type", out JsonElement type) || type.ValueKind != JsonValueKind.String)
        {
            throw new OzoneOperationException("ServiceUnavailable", "The public AppView returned an invalid thread.");
        }
        string? returnedUri = null;
        if (type.GetString() == "app.bsky.feed.defs#threadViewPost" &&
            thread.TryGetProperty("post", out JsonElement post) && post.ValueKind == JsonValueKind.Object &&
            post.TryGetProperty("uri", out JsonElement postUri) && postUri.ValueKind == JsonValueKind.String)
        {
            returnedUri = postUri.GetString();
        }
        else if (type.GetString() is "app.bsky.feed.defs#notFoundPost" or "app.bsky.feed.defs#blockedPost" &&
            thread.TryGetProperty("uri", out JsonElement unavailableUri) && unavailableUri.ValueKind == JsonValueKind.String)
        {
            returnedUri = unavailableUri.GetString();
        }
        if (!string.Equals(uri, returnedUri, StringComparison.Ordinal))
        {
            throw new OzoneOperationException("ServiceUnavailable", "The public AppView returned a different post identity.");
        }
        return payload;
    }

    /// <summary>Lists only the caller's supported personal media option, keeping instance settings unconfigured.</summary>
    private async Task<byte[]> ListOptionsAsync(HttpContext context, LabelAdministrationCaller caller, CancellationToken token)
    {
        ValidateQuery(context, ["limit", "cursor", "scope", "prefix", "keys"]);
        if (context.Request.Query.ContainsKey("scope") && Query(context, "scope") is not ("instance" or "personal") ||
            context.Request.Query.ContainsKey("cursor"))
        {
            throw new JsonException("The setting scope or cursor is invalid.");
        }
        if (context.Request.Query["keys"].Count > 100 || !context.Request.Query.ContainsKey("prefix") &&
            context.Request.Query["keys"].Any(key => key is null || !Nsid.TryParse(key, out _)))
        {
            throw new JsonException("Setting keys must be valid NSIDs.");
        }
        bool matches = Query(context, "scope") == "personal" &&
            (context.Request.Query.ContainsKey("prefix")
                ? MediaPreferenceKey.StartsWith(Query(context, "prefix"), StringComparison.Ordinal)
                : !context.Request.Query.ContainsKey("keys") || context.Request.Query["keys"].Contains(MediaPreferenceKey));
        LabelMediaPreferences? preferences = matches
            ? Require(await MediaPreferenceStore.ReadMediaPreferencesAsync(caller, token).ConfigureAwait(false))
            : null;
        return Encode(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("options");
            if (preferences is not null)
            {
                WriteMediaOption(writer, caller, preferences);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    /// <summary>Identifies the single supported personal setting without claiming stock Ozone uses a server setting for media.</summary>
    private const string MediaPreferenceKey = "dev.idunno.labeler.mediaPreferences";

    /// <summary>Requires durable caller-scoped preference support rather than silently discarding writes.</summary>
    private ILabelTableStore MediaPreferenceStore => store as ILabelTableStore ??
        throw new OzoneOperationException("StorageUnavailable", "This store does not support personal media preferences.");

    /// <summary>Defines the exact Ozone-local preference labels exposed through the personal option value.</summary>
    private static readonly string[] s_mediaPreferenceLabels = ["graphic-media", "porn", "nudity", "sexual", "self-harm"];

    /// <summary>Defines the supported filter names and their shared stored bits.</summary>
    private static readonly (string Name, LabelMediaFilter Filter)[] s_mediaPreferenceFilters =
        [("blur", LabelMediaFilter.Blur), ("grayscale", LabelMediaFilter.Grayscale), ("translucent", LabelMediaFilter.Translucent)];

    /// <summary>Updates the authenticated caller's media option only; no arbitrary settings or target identities are accepted.</summary>
    private async Task<byte[]> UpsertMediaOptionsAsync(HttpContext context, LabelAdministrationCaller caller, CancellationToken token)
    {
        ValidateQuery(context, []);
        byte[] body = await AdministrationWire.ReadAsync(context.Request.Body, 32768, token).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 8 });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Any(property => property.Name is not ("key" or "scope" or "value")) ||
            root.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count())
        {
            throw new JsonException("Only key, scope and value are supported for personal media preferences.");
        }
        if (!root.TryGetProperty("key", out JsonElement key) || key.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("scope", out JsonElement scope) || scope.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("value", out JsonElement value))
        {
            throw new JsonException("A setting key, scope and value are required.");
        }
        if (key.GetString() != MediaPreferenceKey || scope.GetString() != "personal")
        {
            throw new OzoneOperationException("NotImplemented", "Only personal media preferences are supported; other settings cannot be changed.");
        }
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 1 ||
            !value.TryGetProperty("graphicMediaPrefs", out JsonElement filters) || filters.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The value must contain only graphicMediaPrefs.");
        }
        Dictionary<string, LabelMediaFilter> selected = s_mediaPreferenceLabels.ToDictionary(label => label, _ => LabelMediaFilter.None, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in filters.EnumerateObject())
        {
            bool matched = false;
            foreach (string label in s_mediaPreferenceLabels)
            {
                foreach ((string name, LabelMediaFilter filter) in s_mediaPreferenceFilters)
                {
                    if (property.Name != $"graphic-pref-{name}-{label}")
                    {
                        continue;
                    }
                    if (!seen.Add(property.Name) || property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    {
                        throw new JsonException("Media filter values must be unique Boolean properties.");
                    }
                    if (property.Value.GetBoolean())
                    {
                        selected[label] |= filter;
                    }
                    matched = true;
                }
            }
            if (!matched)
            {
                throw new JsonException("The media filter key is not supported.");
            }
        }
        LabelMediaPreferences preferences = Require(await MediaPreferenceStore.SaveMediaPreferencesAsync(
            caller,
            new(
                GraphicMedia: selected["graphic-media"],
                Porn: selected["porn"],
                Nudity: selected["nudity"],
                Sexual: selected["sexual"],
                SelfHarm: selected["self-harm"]),
            token).ConfigureAwait(false));
        return Encode(writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("option");
            WriteMediaOption(writer, caller, preferences);
            writer.WriteEndObject();
        });
    }

    /// <summary>Writes a lexicon-compatible personal option without exposing another member's preferences or inventing timestamps.</summary>
    private static void WriteMediaOption(Utf8JsonWriter writer, LabelAdministrationCaller caller, LabelMediaPreferences preferences)
    {
        writer.WriteStartObject();
        writer.WriteString("$type", "tools.ozone.setting.defs#option");
        writer.WriteString("key", MediaPreferenceKey);
        writer.WriteString("scope", "personal");
        writer.WriteString("did", caller.Actor.Value);
        writer.WriteString("createdBy", caller.Actor.Value);
        writer.WriteString("lastUpdatedBy", caller.Actor.Value);
        writer.WriteStartObject("value");
        writer.WriteStartObject("graphicMediaPrefs");
        foreach (string label in s_mediaPreferenceLabels)
        {
            foreach ((string name, LabelMediaFilter filter) in s_mediaPreferenceFilters)
            {
                writer.WriteBoolean($"graphic-pref-{name}-{label}", preferences.ForLabel(label).HasFlag(filter));
            }
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
