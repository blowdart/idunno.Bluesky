// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Parses bounded query input and binds opaque navigation tokens to the complete normalized filter set.</summary>
/// <param name="Patterns">The normalized OR patterns.</param>
/// <param name="Sources">The normalized source DIDs.</param>
/// <param name="Limit">The requested page size.</param>
/// <param name="Cursor">The optional continuation.</param>
internal sealed record LabelQuery(string[] Patterns, Did[] Sources, int Limit, string? Cursor)
{
    /// <summary>Rejects oversized inputs and duplicate scalars before accessing durable storage.</summary>
    internal static LabelQuery Parse(HttpRequest request)
    {
        if (request.QueryString.Value?.Length > 32768)
        {
            throw new ArgumentException("The query exceeds the hosting input limit.");
        }

        string[] patterns = Values(request, "uriPatterns");
        if (patterns.Length == 0 || patterns.Any(pattern => pattern.IndexOf('*', StringComparison.Ordinal) is int position &&
            position >= 0 && position != pattern.Length - 1))
        {
            throw new ArgumentException("Specify exact subjects or trailing-asterisk patterns.");
        }

        string[] sourceValues = Values(request, "sources");
        List<Did> sources = [];
        foreach (string value in sourceValues)
        {
            if (!Did.TryParse(value, out Did? did))
            {
                throw new ArgumentException("A source is not a valid DID.");
            }

            sources.Add(did);
        }

        string? limitText = Scalar(request, "limit");
        int limit = 50;
        if (limitText is not null && (!int.TryParse(limitText, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 250))
        {
            throw new ArgumentException("The limit must be an integer from 1 through 250.");
        }

        string? cursor = Scalar(request, "cursor");
        if (cursor?.Length > 512)
        {
            throw new ArgumentException("The cursor exceeds the hosting input limit.");
        }

        return new LabelQuery(
            Patterns: patterns,
            Sources: [.. sources],
            Limit: limit,
            Cursor: cursor);
    }

    /// <summary>Reads bounded string arrays without splitting commas or normalizing URI text.</summary>
    private static string[] Values(HttpRequest request, string name)
    {
        Microsoft.Extensions.Primitives.StringValues values = request.Query[name];
        if (values.Count > 100 || values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 2048))
        {
            throw new ArgumentException("The filter exceeds the hosting input limits or contains an empty value.");
        }

        return [.. values.Select(value => value!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>Requires scalar query arguments to occur exactly once when supplied.</summary>
    internal static string? Scalar(HttpRequest request, string name)
    {
        Microsoft.Extensions.Primitives.StringValues values = request.Query[name];
        if (values.Count > 1 || (values.Count == 1 && string.IsNullOrWhiteSpace(values[0])))
        {
            throw new ArgumentException("Scalar parameters must contain exactly one nonempty value.");
        }

        return values.Count == 0 ? null : values[0];
    }

    /// <summary>Hashes length-delimited filters, so array boundaries and source exclusions remain cursor-bound.</summary>
    private string Fingerprint()
    {
        using MemoryStream buffer = new();
        using BinaryWriter writer = new(buffer, Encoding.UTF8);
        writer.Write(Patterns.Length);
        foreach (string pattern in Patterns)
        {
            writer.Write(pattern);
        }

        writer.Write(Sources.Length);
        foreach (Did source in Sources)
        {
            writer.Write(source.Value);
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }

    /// <summary>Captures a stable published snapshot or validates a prior navigation token against current retention.</summary>
    internal (long After, long Through) Position(LabelStoreState state)
    {
        if (Cursor is null)
        {
            return (state.ReplayFloor, state.PublishedThrough);
        }

        try
        {
            string[] fields = Encoding.UTF8.GetString(Convert.FromBase64String(Cursor)).Split('|');
            if (fields.Length != 5 || fields[0] != "1" || fields[1] != state.StreamId.ToString("N") ||
                fields[4] != Fingerprint() ||
                !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out long through) ||
                !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out long after) ||
                after < state.ReplayFloor || through < after || through > state.PublishedThrough)
            {
                throw new ArgumentException("The cursor is invalid, incompatible or no longer retained.");
            }

            return (after, through);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The cursor is malformed.", exception);
        }
    }

    /// <summary>Encodes a continuation without treating it as an authorization capability.</summary>
    internal string Continuation(Guid streamId, long through, long after) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"1|{streamId:N}|{through}|{after}|{Fingerprint()}")));

    /// <summary>Wraps exact signed JSON in the lexicon response without rebuilding signed fields.</summary>
    internal byte[] Encode(LabelDistributionPage page, Guid streamId, long through, int maximumBytes)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            if (page.ScannedThrough < through)
            {
                writer.WriteString("cursor", Continuation(streamId, through, page.ScannedThrough));
            }

            writer.WriteStartArray("labels");
            foreach (StoredLabel label in page.Labels)
            {
                byte[] json = label.SignedLabel.GetJsonBytes();
                if (buffer.Length + writer.BytesPending + json.Length > maximumBytes)
                {
                    throw new InvalidDataException("The query response exceeds the hosting message limit.");
                }

                writer.WriteRawValue(json);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        if (buffer.Length > maximumBytes)
        {
            throw new InvalidDataException("The query response exceeds the hosting message limit.");
        }

        return buffer.ToArray();
    }
}
