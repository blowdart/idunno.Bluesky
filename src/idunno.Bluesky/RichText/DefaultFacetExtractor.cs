// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.RegularExpressions;

using idunno.AtProto;

namespace idunno.Bluesky.RichText;

/// <summary>
/// Extracts facets from a supplied string.
/// </summary>
public sealed partial class DefaultFacetExtractor : IFacetExtractor
{
    // Regexes and logic taken from https://docs.bsky.app/docs/advanced-guides/post-richtext

    private readonly Func<string, CancellationToken, Task<Did?>> _resolveHandle;

    // Punctuation which is trimmed from the end of an extracted link. See ExtractUris.
    private static readonly SearchValues<char> s_trailingPunctuation = SearchValues.Create(".,;:!?");

    [GeneratedRegex(@"(?:^|\s)(#[^\d\s]\S*)", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex s_HashTagRegex();

    [GeneratedRegex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+,.~#&\/=]*)(\?[-a-zA-Z0-9()@:%_\+,.~#&\/=]+)?", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex s_UrlRegex();

    [GeneratedRegex(@"(?:^|\s|\()(@\w+(?:\.\w+)*)", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex s_MentionRegex();

    [GeneratedRegex(@"(?:^|\s|\()\$([A-Za-z][A-Za-z0-9]{0,4})(?=\s|$|[.,;:!?)""'’])", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex s_CashTagRegex();

    /// <summary>
    /// Construct a new instance of <see cref="DefaultFacetExtractor"/>.
    /// </summary>
    /// <param name="resolveHandle">A <see cref="Func{T1, T2, TResult}"/> that returns a DID for a handle.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="resolveHandle"/> is <see langword="null"/>.</exception>
    public DefaultFacetExtractor(Func<string, CancellationToken, Task<Did?>> resolveHandle)
    {
        ArgumentNullException.ThrowIfNull(resolveHandle);

        _resolveHandle = resolveHandle;
    }

    /// <summary>
    /// Extracts facets from the specified <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text to extract any facets from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="RegexMatchTimeoutException">Thrown if matching a pattern against <paramref name="text"/> exceeds the one second match timeout.</exception>
    /// <remarks>
    /// <para>Every distinct handle mentioned in <paramref name="text"/> is resolved, which is typically a network
    /// call, so the work this does grows with the length of <paramref name="text"/>. Callers which accept text from
    /// elsewhere should apply their own length limit before calling this.</para>
    /// </remarks>
    public async Task<IList<Facet>> ExtractFacets(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<Facet> facets = [];

        List<Facet> hashTagFacets = ExtractHashTags(text);
        if (hashTagFacets.Count > 0)
        {
            facets.AddRange(hashTagFacets);
        }

        List<Facet> cashTagFacets = ExtractCashTags(text);
        if (cashTagFacets.Count > 0)
        {
            facets.AddRange(cashTagFacets);
        }

        List<Facet> linkFacets = ExtractUris(text);
        if (linkFacets.Count > 0)
        {
            facets.AddRange(linkFacets);
        }

        List<Facet> mentionFacets = await ExtractMentionsAndResolveHandles(text, _resolveHandle, cancellationToken).ConfigureAwait(false);
        if (mentionFacets.Count > 0)
        {
            facets.AddRange(mentionFacets);
        }

        // Facets are extracted a type at a time, so the list is grouped by facet type rather than ordered by
        // position in the text. Sorting by the starting byte puts them into document order, which is the order
        // the AT Protocol expects and which clients rely on when they walk the text a facet at a time.
        facets.Sort(static (x, y) => x.Index.ByteStart.CompareTo(y.Index.ByteStart));

        return facets;
    }

    /// <remarks>
    /// <para>
    ///   Note the asymmetry with <see cref="ExtractCashTags(string)"/>: a hash tag's feature value has its <c>#</c>
    ///   stripped, whereas a cash tag's feature value keeps its <c>$</c>. This is deliberate and matches Bluesky,
    ///   where <c>#tag</c> and <c>$tag</c> are distinct tags.
    /// </para>
    /// </remarks>
    private static List<Facet> ExtractHashTags(string text)
    {
        List<Facet> hashTags = [];
        MatchCollection hashTagMatches = s_HashTagRegex().Matches(text);

        foreach (Match match in hashTagMatches)
        {
            // Group 1 excludes any leading whitespace the pattern consumed, so its index and
            // value always refer to the tag itself.
            Group tagGroup = match.Groups[1];

            // This will have the # prefix.
            string extractedTag = tagGroup.Value;

            // Strip trailing punctuation
            // The length check stops cases like #! from ending up in index errors, because, of course, # is also a punctuation mark.
            while (extractedTag.Length > 1 && char.IsPunctuation(extractedTag[extractedTag.Length - 1]))
            {
                extractedTag = extractedTag.Substring(0, extractedTag.Length - 1);
            }

            if (extractedTag.Length <= 1)
            {
                continue;
            }

            // The facet feature value excludes the hash prefix, so the lexicon limits apply to the text after it.
            // These mirror the validation in TagFacetFeature, so an over-long tag is skipped rather than throwing.
            string tagValue = extractedTag[1..];

            if (tagValue.GetUtf8Length() > Maximum.TagLengthInBytes ||
                tagValue.GetGraphemeLength() > Maximum.TagLengthInGraphemes)
            {
                continue;
            }

            TagFacetFeature tagFacetFeature = new(tagValue);

            ByteSlice index = new(text.GetUtf8BytePosition(tagGroup.Index), text.GetUtf8BytePosition(tagGroup.Index + extractedTag.Length));
            hashTags.Add(new Facet(index, [tagFacetFeature]));
        }

        return hashTags;
    }

    /// <remarks>
    /// <para>
    ///   Note the asymmetry with <see cref="ExtractHashTags(string)"/>: a hash tag's feature value has its <c>#</c>
    ///   stripped, whereas a cash tag's feature value keeps its <c>$</c>. This is deliberate and matches Bluesky,
    ///   where <c>#tag</c> and <c>$tag</c> are distinct tags. Stripping the <c>$</c> would make a cash tag
    ///   indistinguishable from the hash tag of the same name.
    /// </para>
    /// </remarks>
    private static List<Facet> ExtractCashTags(string text)
    {
        List<Facet> cashTags = [];

        MatchCollection cashTagMatches = s_CashTagRegex().Matches(text);

        foreach (Match match in cashTagMatches)
        {
            // Group 1 is the symbol without its $ prefix. The pattern may also consume a leading
            // whitespace character or opening parenthesis, neither of which belongs in the facet.
            Group symbolGroup = match.Groups[1];

            // The $ prefix always sits immediately before the captured symbol, and is retained in the
            // tag value. See the remarks on this method.
            int start = symbolGroup.Index - 1;
            string extractedTag = text.Substring(start, symbolGroup.Length + 1);

            TagFacetFeature tagFacetFeature = new(extractedTag);

            ByteSlice index = new(text.GetUtf8BytePosition(start), text.GetUtf8BytePosition(start + extractedTag.Length));
            cashTags.Add(new Facet(index, [tagFacetFeature]));
        }

        return cashTags;
    }

    private static List<Facet> ExtractUris(string text)
    {
        List<Facet> links = [];
        MatchCollection matches = s_UrlRegex().Matches(text);

        foreach (Match match in matches)
        {
            string uri = match.Value;

            // Trailing punctuation is far more likely to be the punctuation of the sentence the link sits in than
            // part of the link itself, so it is trimmed from both the uri and the facet's byte range. This mirrors
            // the behavior of the official Bluesky clients.
            if (uri.Length > 0 && s_trailingPunctuation.Contains(uri[^1]))
            {
                uri = uri[..^1];
            }

            // A closing parenthesis only belongs to the link if the link also contains an opening one, so that
            // a link wrapped in parentheses drops the closing one, but a link such as
            // https://en.wikipedia.org/wiki/Foo_(bar) keeps it.
            if (uri.EndsWith(')') && !uri.Contains('(', StringComparison.Ordinal))
            {
                uri = uri[..^1];
            }

            if (uri.Length == 0)
            {
                continue;
            }

            LinkFacetFeature linkFacetFeature = new(uri);

            // Only trailing characters are trimmed, so the start of the match is still the start of the link.
            ByteSlice index = new(text.GetUtf8BytePosition(match.Index), text.GetUtf8BytePosition(match.Index + uri.Length));
            links.Add(new Facet(index, [linkFacetFeature]));
        }

        return links;
    }

    private static async Task<List<Facet>> ExtractMentionsAndResolveHandles(
        string text,
        Func<string, CancellationToken, Task<Did?>> resolveHandle,
        CancellationToken cancellationToken = default)
    {
        List<Facet> mentions = [];
        MatchCollection matches = s_MentionRegex().Matches(text);

        // The same handle can be mentioned more than once in a single piece of text, and resolving a handle is a
        // network call, so every distinct handle is resolved at most once. Handles which do not resolve are recorded
        // too, otherwise a handle which does not exist would be looked up again for each time it appears.
        Dictionary<string, Did?> resolvedHandles = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            // Group 1 excludes any leading whitespace or opening parenthesis the pattern consumed.
            Group mentionGroup = match.Groups[1];

            string handle = mentionGroup.Value[1..];

            if (Handle.TryParse(handle, out _))
            {
                if (!resolvedHandles.TryGetValue(handle, out Did? did))
                {
                    did = await resolveHandle(handle, cancellationToken).ConfigureAwait(false);
                    resolvedHandles[handle] = did;
                }

                if (did is not null)
                {
                    MentionFacetFeature mentionFacetFeature = new(did);
                    ByteSlice index = new(text.GetUtf8BytePosition(mentionGroup.Index), text.GetUtf8BytePosition(mentionGroup.Index + mentionGroup.Length));
                    mentions.Add(new Facet(index, [mentionFacetFeature]));
                }
            }
        }

        return mentions;
    }
}