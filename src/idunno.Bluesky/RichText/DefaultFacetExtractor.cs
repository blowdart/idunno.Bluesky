// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

using idunno.AtProto;

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Extracts facets from a supplied string.
    /// </summary>
    public sealed partial class DefaultFacetExtractor : IFacetExtractor
    {
        // Regexes and logic taken from https://docs.bsky.app/docs/advanced-guides/post-richtext

        private readonly Func<string, CancellationToken, Task<Did?>> _resolveHandle;

        [GeneratedRegex(@"(?:^|\s)(#[^\d\s]\S*)(?=\s)?", RegexOptions.IgnoreCase, 5000)]
        private static partial Regex s_HashTagRegex();

        [GeneratedRegex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+,.~#&\/=]*)", RegexOptions.IgnoreCase, 5000)]
        private static partial Regex s_UrlRegex();

        [GeneratedRegex(@"@\w+(\.\w+)*", RegexOptions.IgnoreCase, 5000)]
        private static partial Regex s_MentionRegex();

        [GeneratedRegex(@"(^|\s|\()\$([A-Za-z][A-Za-z0-9]{0,4})(?=\s|$|[.,;:!?)""'’])", RegexOptions.IgnoreCase, 5000)]
        private static partial Regex s_CashTagRegex();

        /// <summary>
        /// Construct a new instance of <see cref="DefaultFacetExtractor"/>.
        /// </summary>
        /// <param name="resolveHandle">A <see cref="Func{T1, T2, TResult}"/> that returns a DID for a handle.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="resolveHandle"/> is null.</exception>
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
        public async Task<IList<Facet>> ExtractFacets(string text, CancellationToken cancellationToken = default)
        {
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

            return facets;
        }

        private static List<Facet> ExtractHashTags(string text)
        {
            List<Facet> hashTags = [];
            MatchCollection hashTagMatches = s_HashTagRegex().Matches(text);

            foreach (Match match in hashTagMatches)
            {
                // This will have the # prefix.
                string extractedTag = match.Value;

                // Strip trailing punctuation
                // The length check stops cases like #! from ending up in index errors, because, of course, # is also a punctuation mark.
                while (extractedTag.Length > 1 && char.IsPunctuation(extractedTag[extractedTag.Length - 1]))
                {
                    extractedTag = extractedTag.Substring(0, extractedTag.Length - 1);
                }

                if (extractedTag.Length <= 1 )
                {
                    break;
                }

                int offset = 0;
                if (match.Value[0] == ' ')
                {
                    offset = 1;
                    extractedTag = extractedTag[1..];
                }

                // Max tag length is 64.
                // Hash prefix is still present so need to account for that.
                if (extractedTag.GetUtf8Length() > 65)
                {
                    break;
                }

                TagFacetFeature tagFacetFeature = new(extractedTag[1..]);

                ByteSlice index = new(text.GetUtf8BytePosition(match.Index + offset), text.GetUtf8BytePosition(match.Index + offset + extractedTag.Length));
                hashTags.Add(new Facet(index, [tagFacetFeature]));
            }

            return hashTags;
        }

        private static List<Facet> ExtractCashTags(string text)
        {
            List<Facet> cashTags = [];

            MatchCollection cashTagMatches = s_CashTagRegex().Matches(text);

            foreach (Match match in cashTagMatches)
            {
                // This will have the $ prefix.
                string extractedTag = match.Value;

                if (extractedTag.Length <=2)
                {
                    break;
                }

                int offset = 0;
                if (match.Value[0] == ' ')
                {
                    offset = 1;
                    extractedTag = extractedTag[1..];
                }

                TagFacetFeature tagFacetFeature = new(extractedTag);

                ByteSlice index = new(text.GetUtf8BytePosition(match.Index + offset), text.GetUtf8BytePosition(match.Index + offset + extractedTag.Length));
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
                LinkFacetFeature tagFacetFeature = new(new Uri(match.Value));
                ByteSlice index = new(text.GetUtf8BytePosition(match.Index), text.GetUtf8BytePosition(match.Index + match.Length));
                links.Add(new Facet(index, [tagFacetFeature]));
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

            foreach(Match match in matches)
            {
                string handle = match.Value[1..];

                if (Handle.TryParse(handle, out _))
                {
                    Did? did = await resolveHandle(handle, cancellationToken).ConfigureAwait(false);

                    if (did is not null)
                    {
                        MentionFacetFeature mentionFacetFeature = new(did);
                        ByteSlice index = new(text.GetUtf8BytePosition(match.Index), text.GetUtf8BytePosition(match.Index + match.Length));
                        mentions.Add(new Facet(index, [mentionFacetFeature]));
                    }
                }
            }

            return mentions;
        }
    }
}
