// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Detects content which looks like credentials, so that it is never written to, or left in, a checked in corpus.
/// </summary>
/// <remarks>
/// <para>The firehose and jetstream are public and unauthenticated, and the capture only records message payloads, so
/// no authorization header can be captured. This is a second line of defence against a credential which a user has
/// pasted into a public post, or a future capture which records something it should not. It is deliberately broad, a
/// message which is rejected is simply replaced by the next one.</para>
/// </remarks>
internal static partial class SensitiveContent
{
    public static bool IsSensitive(ReadOnlySpan<byte> utf8) => Pattern().IsMatch(Encoding.Latin1.GetString(utf8));

    [GeneratedRegex(
        @"authori[sz]ation|bearer\s|dpop|access_?jwt|refresh_?jwt|access_token|refresh_token|id_token|client_secret|app[_-]?password|eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 10000)]
    private static partial Regex Pattern();
}