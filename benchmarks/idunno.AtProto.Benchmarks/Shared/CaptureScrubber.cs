// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Removes anything which identifies the capturing account from an authenticated response before it is written to a corpus.
/// </summary>
internal static class CaptureScrubber
{
    /// <summary>
    /// The DID which replaces the capturing account's DID in a scrubbed response.
    /// </summary>
    public const string PlaceholderDid = "did:plc:aaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>
    /// The environment variable holding the handle of the account authenticated captures use.
    /// </summary>
    public const string HandleVariable = "_BlueskyHandle";

    /// <summary>
    /// The environment variable holding the password, or app password, of the account authenticated captures use.
    /// </summary>
    public const string PasswordVariable = "_BlueskyPassword";

    /// <summary>
    /// Gets the non-empty values of the <see cref="HandleVariable"/> and <see cref="PasswordVariable"/> environment variables.
    /// </summary>
    public static IReadOnlyList<string> CredentialsFromEnvironment() =>
        [.. new[] { HandleVariable, PasswordVariable }
            .Select(Environment.GetEnvironmentVariable)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)];

    /// <summary>
    /// Removes every entry of the <c>feed</c> array which mentions any of the <paramref name="secrets"/>, then replaces
    /// every remaining occurrence of the <paramref name="viewerDid"/>, for example in viewer state, with <see cref="PlaceholderDid"/>.
    /// </summary>
    /// <remarks>
    /// <para>Kept entries are copied byte for byte, so the scrubbed response is encoded exactly as the server sent it.</para>
    /// </remarks>
    public static byte[] ScrubFeed(ReadOnlySpan<byte> utf8Json, string viewerDid, IReadOnlyCollection<string> secrets, out int removed)
    {
        removed = 0;

        Utf8JsonReader reader = new(utf8Json);
        int arrayStart = -1;
        int arrayEnd = -1;
        List<Range> kept = [];

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1 && reader.ValueTextEquals("feed"u8))
            {
                reader.Read();
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    reader.Skip();
                    continue;
                }

                arrayStart = (int)reader.BytesConsumed;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    int start = (int)reader.TokenStartIndex;
                    reader.Skip();
                    Range entry = start..(int)reader.BytesConsumed;

                    if (ContainsAny(utf8Json[entry], secrets))
                    {
                        removed++;
                    }
                    else
                    {
                        kept.Add(entry);
                    }
                }

                arrayEnd = (int)reader.TokenStartIndex;
            }
        }

        if (arrayStart < 0)
        {
            throw new InvalidOperationException("The response does not contain a feed array.");
        }

        ArrayBufferWriter<byte> buffer = new(utf8Json.Length);
        buffer.Write(utf8Json[..arrayStart]);
        for (int i = 0; i < kept.Count; i++)
        {
            if (i > 0)
            {
                buffer.Write(","u8);
            }

            buffer.Write(utf8Json[kept[i]]);
        }

        buffer.Write(utf8Json[arrayEnd..]);

        string scrubbed = Encoding.UTF8.GetString(buffer.WrittenSpan).Replace(viewerDid, PlaceholderDid, StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(scrubbed);
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="utf8"/> contains any of the <paramref name="values"/>, ignoring case.
    /// </summary>
    /// <remarks>
    /// <para>The raw text is searched and, when <paramref name="utf8"/> is JSON, so is every unescaped property name and
    /// string value, so a value is found even if the server escaped some of its characters.</para>
    /// </remarks>
    public static bool ContainsAny(ReadOnlySpan<byte> utf8, IEnumerable<string> values)
    {
        string[] candidates = [.. values.Where(value => !string.IsNullOrEmpty(value))];
        if (candidates.Length == 0)
        {
            return false;
        }

        if (ContainsAny(Encoding.UTF8.GetString(utf8), candidates))
        {
            return true;
        }

        try
        {
            Utf8JsonReader reader = new(utf8);
            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName &&
                    reader.ValueIsEscaped &&
                    ContainsAny(reader.GetString()!, candidates))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON, for example a firehose CBOR frame, whose strings are raw UTF-8 and were searched above.
        }

        return false;
    }

    private static bool ContainsAny(string text, string[] values)
    {
        foreach (string value in values)
        {
            if (text.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}