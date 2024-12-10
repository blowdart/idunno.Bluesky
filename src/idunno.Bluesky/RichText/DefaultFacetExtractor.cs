// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;
using System.Runtime.Caching;
using System.Text.RegularExpressions;

using idunno.AtProto;

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Extracts facets from a supplied string.
    /// </summary>
    public sealed partial class DefaultFacetExtractor : IFacetExtractor, IDisposable
    {
        private volatile bool _disposed;

        private static readonly NameValueCollection s_cacheConfig = new()
        {
            { "cacheMemoryLimitMegabytes", "5" },
            { "pollingInterval", "00:05:00" }
        };

        private readonly MemoryCache _handleResolutionCache = new("idunno.Bluesky.handleResolutionCache", s_cacheConfig);

        private readonly Func<string, CancellationToken, Task<Did?>> _resolveHandle;

        [GeneratedRegex(@"#[a-z0-9_]+", RegexOptions.IgnoreCase, 5000)]
        private static partial Regex s_HashTagRegex();

        [GeneratedRegex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)", RegexOptions.IgnoreCase, 5000)]
        private static partial Regex s_UrlRegex();

        [GeneratedRegex(@"@\w+(\.\w+)*", RegexOptions.IgnoreCase, 5000)]
        private static partial Regex s_MentionRegex();

        /// <summary>
        /// Construct a new instance of <see cref="DefaultFacetExtractor"/>.
        /// </summary>
        /// <param name="resolveHandle">A <see cref="Func{T1, T2, TResult}"/> that returns a DID for a handle.</param>
        public DefaultFacetExtractor(Func<string, CancellationToken, Task<Did?>> resolveHandle)
        {
            ArgumentNullException.ThrowIfNull(resolveHandle);

            _resolveHandle = resolveHandle;
        }

        /// <summary>
        /// Releases all resources that are used by the current instance of the DefaultFacetExtractor class.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Extracts facets from the specified <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The text to extract any facets from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<IList<Facet>> ExtractFacets(string text, CancellationToken cancellationToken = default)
        {
            List<Facet> facets = new();

            List<Facet> hashTagFacets = ExtractHashTags(text);
            if (hashTagFacets.Count > 0)
            {
                facets.AddRange(hashTagFacets);
            }

            List<Facet> linkFacets = ExtractUris(text);
            if (linkFacets.Count > 0)
            {
                facets.AddRange(linkFacets);
            }

            List<Facet> mentionFacets = await ExtractMentionsAndResolveHandles(text, _resolveHandle, _handleResolutionCache, cancellationToken).ConfigureAwait(false);
            if (mentionFacets.Count > 0)
            {
                facets.AddRange(mentionFacets);
            }

            return facets;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _handleResolutionCache.Dispose();
                }

                _disposed = true;
            }
        }

        private static List<Facet> ExtractHashTags(string text)
        {
            List<Facet> hashTags = new();
            MatchCollection matches = s_HashTagRegex().Matches(text);

            foreach (Match match in matches)
            {
                TagFacetFeature tagFacetFeature = new(match.Value[1..]);
                ByteSlice index = new(text.GetUtf8BytePosition(match.Index), text.GetUtf8BytePosition(match.Index + match.Length));
                hashTags.Add(new Facet(index, new List<FacetFeature> { tagFacetFeature }));
            }

            return hashTags;
        }

        private static List<Facet> ExtractUris(string text)
        {
            List<Facet> links = new();
            MatchCollection matches = s_UrlRegex().Matches(text);

            foreach (Match match in matches)
            {
                LinkFacetFeature tagFacetFeature = new(new Uri(match.Value));
                ByteSlice index = new(text.GetUtf8BytePosition(match.Index), text.GetUtf8BytePosition(match.Index + match.Length));
                links.Add(new Facet(index, new List<FacetFeature> { tagFacetFeature }));
            }

            return links;
        }

        private static async Task<List<Facet>> ExtractMentionsAndResolveHandles(
            string text,
            Func<string, CancellationToken, Task<Did?>> resolveHandle,
            MemoryCache cache,
            CancellationToken cancellationToken = default)
        {
            List<Facet> mentions = new();
            MatchCollection matches = s_MentionRegex().Matches(text);

            foreach(Match match in matches)
            {
                string handle = match.Value[1..];

                if (Handle.TryParse(handle, out _))
                {
                    // Memory Cache doesn't allow null values, so convert the DID to a string if found, or use string.Empty if not.
                    string? didAsString;

                    if (!cache.Contains(handle))
                    {
                        Did? did = await resolveHandle(handle, cancellationToken).ConfigureAwait(false);

                        if (did is not null)
                        {
                            didAsString = did.ToString();
                        }
                        else
                        {
                            didAsString = string.Empty;
                        }

                        CacheItemPolicy cachePolicy = new()
                        {
                            AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(5)
                        };

                        cache.Add(new CacheItem(handle, didAsString), cachePolicy);
                    }
                    else
                    {
                        didAsString = cache[handle] as string;
                    }

                    if (!string.IsNullOrEmpty(didAsString))
                    {
                        MentionFacetFeature mentionFacetFeature = new(new Did(didAsString));
                        ByteSlice index = new(text.GetUtf8BytePosition(match.Index), text.GetUtf8BytePosition(match.Index + match.Length));
                        mentions.Add(new Facet(index, new List<FacetFeature> { mentionFacetFeature }));
                    }
                }
            }

            return mentions;
        }
    }
}
